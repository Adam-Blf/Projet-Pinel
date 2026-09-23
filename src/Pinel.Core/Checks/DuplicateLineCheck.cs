using System.Security.Cryptography;
using System.Text;
using Pinel.Core.Formats;

namespace Pinel.Core.Checks;

/// <summary>
/// Detects strictly identical lines inside the same ATIH file. Exact
/// duplicates are usually the result of a re-run of the SIH export that
/// appended records to an already-submitted file. DRUIDES does not
/// always reject doublons - OVALIDE inflates the activity without
/// warning and the valorisation PSY (VAP) is biased upward.
/// <para>
/// Exception du RAA : une ligne ne porte ni heure ni identifiant d'acte, deux
/// entretiens identiques le meme jour donnent deux lignes identiques. Sur un
/// lot reel accepte par e-PMSI (22/09/2026), 13 % des lignes RAA etaient dans
/// ce cas. Le volume y reste signale, en avertissement et non en erreur.
/// </para>
/// </summary>
public sealed class DuplicateLineCheck : IFileCheck
{

    public string Name => "Doublons de lignes";

    /// <summary>Applies to all fixed-width ATIH formats (null = any).</summary>
    public IReadOnlySet<string>? AppliesTo => null;


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

        // Map hash → first line number where the hash was seen. Keep it compact
        // with an 8-byte (64-bit) truncated SHA-256 so a 1M-line file stays <16 MB.
        var seen = new Dictionary<ulong, int>(capacity: 4096);
        int reported = 0;
        int totalDuplicates = 0;

        using (reader)
        {
            int lineNo = 0;
            while (reader.ReadLine() is { } line)
            {
                lineNo++;
                if (line.Length < 20) continue; // ignore padding / blank

                var hash = Hash64(line);
                if (seen.TryGetValue(hash, out var first))
                {
                    totalDuplicates++;
                    if (reported < 20)
                    {
                        reported++;
                        yield return new CheckFinding(
                            "WARN-DOUBLON-LIGNE", CheckSeverity.Warning,
                            $"Ligne {lineNo} identique à la ligne {first} - doublon strict.",
                            filePath, lineNo, formatName,
                            FixHint: "Vérifier re-execution de l'export SIH ou concaténation accidentelle.");
                    }
                }
                else
                {
                    seen[hash] = lineNo;
                }
            }
        }

        if (totalDuplicates > 20)
        {
            bool raa = string.Equals(formatName, "RAA", StringComparison.OrdinalIgnoreCase);
            yield return raa
                ? new CheckFinding(
                    "WARN-DOUBLON-BULK", CheckSeverity.Warning,
                    $"{totalDuplicates} lignes RAA identiques : actes répétés le même jour, ou export relancé.",
                    filePath, 0, formatName,
                    FixHint: "Un RAA ne distingue pas deux actes identiques du même jour : ne dédupliquer qu'après vérification du dossier.")
                : new CheckFinding(
                    "ERR-DOUBLON-BULK", CheckSeverity.Error,
                    $"{totalDuplicates} lignes dupliquées dans le fichier - biais d'activité probable.",
                    filePath, 0, formatName,
                    FixHint: "Dé-dupliquer avant envoi DRUIDES pour ne pas fausser la VAP ou l'indicateur de file active.");
        }
    }

    /// <summary>
    /// 64-bit truncated SHA-256 - suffisant pour éviter les collisions
    /// sur des fichiers ATIH (probabilité &lt; 10⁻⁹ à 1 M lignes).
    /// </summary>
    private static ulong Hash64(string line)
    {
        var bytes = Encoding.UTF8.GetBytes(line);
        Span<byte> digest = stackalloc byte[32];
        SHA256.HashData(bytes, digest);
        return BitConverter.ToUInt64(digest[..8]);
    }
}
