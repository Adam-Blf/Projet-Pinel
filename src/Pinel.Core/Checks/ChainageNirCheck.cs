using System.Text;
using Pinel.Core.Formats;

namespace Pinel.Core.Checks;

/// <summary>
/// Detects the two most frequent chaînage failure modes documented in the
/// SNDS PSY catalogue:
/// <list type="bullet">
///   <item><c>ERR-VID-NIR-MISSING</c> (retour 999999) - NIR absent -
///     premier motif de rejet PSY (~70% des échecs pour patients
///     institutionnalisés de longue date).</item>
///   <item><c>ERR-VID-NIR-FORMAT</c> (retour 400000) - NIR non numérique
///     ou longueur non conforme.</item>
/// </list>
/// Le NIR occupe les 13 premiers caractères de la ligne VID-HOSP / VID-IPP,
/// suivis d'une clé de 2 chiffres.
/// </summary>
public sealed class ChainageNirCheck : IFileCheck
{

    /// <summary>NIR occupies the first 13 characters of the record.</summary>
    private const int NirLength = 13;

    /// <summary>The 2-digit control key follows the NIR.</summary>
    private const int KeyLength = 2;

    public string Name => "NIR VID-HOSP";

    public IReadOnlySet<string>? AppliesTo { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "VID-HOSP", "ANO-HOSP",
    };


    public IEnumerable<CheckFinding> Validate(string filePath, string formatName)
    {
        StreamReader? reader = null;
        string? readFailure = null;
        try { reader = new StreamReader(filePath, PmsiEncoding.Latin1); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            readFailure = FindingRedaction.ReadFailure(ex);
        }

        if (reader is null)
        {
            yield return new CheckFinding(
                "ERR-IO", CheckSeverity.Blocker,
                readFailure ?? FindingRedaction.ReadFailedMessage,
                filePath, 0, formatName,
                FixHint: FindingRedaction.ReadFailureHint);
            yield break;
        }

        using (reader)
        {
            int lineNo = 0;
            int missingCount = 0;
            while (reader.ReadLine() is { } line)
            {
                lineNo++;
                if (line.Length < NirLength + KeyLength) continue;

                var nir = line[..NirLength].Trim();
                var key = line[NirLength..(NirLength + KeyLength)].Trim();

                if (nir.Length == 0 || nir.All(c => c == '0'))
                {
                    missingCount++;
                    // Cap individual findings, emit aggregate at the end.
                    if (missingCount <= 20)
                    {
                        yield return new CheckFinding(
                            "ERR-VID-NIR-MISSING", CheckSeverity.Warning,
                            "NIR absent - chaînage retournera code 999999 (pas de numéro anonyme).",
                            filePath, lineNo, formatName,
                            FixHint: "Vérifier saisie NIR patient dans le SIH, ou statuer en DIM comme cas long-séjour sans SS.");
                    }
                    continue;
                }

                // Message carries the GAP, never the NIR: findings reach the UI
                // and the JSON report on disk. See FindingRedaction.
                if (nir.Length != NirLength || !nir.All(char.IsDigit))
                {
                    yield return new CheckFinding(
                        "ERR-VID-NIR-FORMAT", CheckSeverity.Error,
                        $"NIR non conforme ({FindingRedaction.Position(0, NirLength)}) : "
                        + $"{FindingRedaction.DigitGap(NirLength, nir)}. "
                        + "Chaînage retournera le code 400000.",
                        filePath, lineNo, formatName,
                        FixHint: "Vérifier l'export NIR (espaces résiduels, clé mal alignée).");
                }

                if (key.Length > 0 && !key.All(char.IsDigit))
                {
                    yield return new CheckFinding(
                        "ERR-VID-NIR-KEY", CheckSeverity.Error,
                        $"Clé de contrôle NIR non conforme ({FindingRedaction.Position(NirLength, KeyLength)}) : "
                        + $"{FindingRedaction.DigitGap(KeyLength, key)}.",
                        filePath, lineNo, formatName,
                        FixHint: "Vérifier que l'export inclut bien la clé 2-chiffres après le NIR 13-chiffres.");
                }
            }

            if (missingCount > 20)
            {
                yield return new CheckFinding(
                    "ERR-VID-NIR-MISSING-BULK", CheckSeverity.Warning,
                    $"{missingCount} lignes VID-HOSP sans NIR - volume anormal.",
                    filePath, 0, formatName,
                    FixHint: "Revue DIM requise - possible export tronqué ou établissement mal configuré.");
            }
        }
    }
}
