using System.Text;
using System.Text.RegularExpressions;
using Pinel.Core.Formats;

namespace Pinel.Core.Checks;

/// <summary>
/// Checks that every record carries a plausible 9-digit FINESS code.
/// DRUIDES and the ATIH e-PMSI portal reject files where the FINESS is
/// missing, shorter/longer than 9 digits, or contains non-digit characters.
/// La position du FINESS depend du format. Elle est prise dans les
/// descriptifs ATIH 2026 : en tete pour RPS, RAA et FICHCOMP, apres le type
/// d'enregistrement pour RSF, en 53-61 pour VID-HOSP et VID-IPP, qui
/// commencent par le NIR. Lire le FINESS en tete d'un VID-HOSP revenait a
/// controler le NIR sous le nom de FINESS (constate le 22/09/2026 sur un lot
/// reel). ANO-HOSP est retire : sa position n'est etablie sur aucun
/// descriptif officiel.
/// </summary>
public sealed class FinessCheck : IFileCheck
{
    private static readonly Regex NineDigits = new("^[0-9]{9}$", RegexOptions.Compiled);

    /// <summary>A FINESS code is 9 digits, at the head of the ATIH record.</summary>
    private const int FinessLength = 9;

    public string Name => "FINESS format";

    /// <summary>Position (index 0) du FINESS d'inscription e-PMSI, par format.</summary>
    private static readonly IReadOnlyDictionary<string, int> FinessOffset =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["RPS"] = 0, ["RAA"] = 0, ["RPSA"] = 0, ["R3A"] = 0,
            ["FICHCOMP"] = 0, ["FICHCOMP-ISO"] = 0, ["FICHCOMP-TP"] = 0,
            ["RSF-ACE-PSY"] = 1,
            ["VID-HOSP"] = 52, ["VID-IPP"] = 52,
        };

    public IReadOnlySet<string>? AppliesTo { get; } =
        new HashSet<string>(FinessOffset.Keys, StringComparer.OrdinalIgnoreCase);


    public IEnumerable<CheckFinding> Validate(string filePath, string formatName)
    {
        if (!AtihMatrix.All.ContainsKey(formatName) || !FinessOffset.TryGetValue(formatName, out var offset))
        {
            yield break;
        }

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
            while (reader.ReadLine() is { } line)
            {
                lineNo++;
                if (line.Length < offset + FinessLength)
                {
                    continue;
                }
                var finess = line.Substring(offset, FinessLength).Trim();
                if (finess.Length == 0)
                {
                    continue; // common padding line
                }
                if (!NineDigits.IsMatch(finess))
                {
                    // Message carries the GAP, never the FINESS: findings reach the
                    // UI and the JSON report on disk. See FindingRedaction.
                    yield return new CheckFinding(
                        "ERR-FINESS-FORMAT", CheckSeverity.Error,
                        $"N° FINESS invalide ({FindingRedaction.Position(offset, FinessLength)}) : "
                        + $"{FindingRedaction.DigitGap(FinessLength, finess)}.",
                        filePath, lineNo, formatName,
                        FixHint: "Vérifier le paramétrage FINESS dans le logiciel métier.");
                }
            }
        }
    }
}
