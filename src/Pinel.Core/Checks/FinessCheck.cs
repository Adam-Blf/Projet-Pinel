using System.Text;
using System.Text.RegularExpressions;
using Pinel.Core.Formats;

namespace Pinel.Core.Checks;

/// <summary>
/// Checks that every record carries a plausible 9-digit FINESS code.
/// DRUIDES and the ATIH e-PMSI portal reject files where the FINESS is
/// missing, shorter/longer than 9 digits, or contains non-digit characters.
/// The FINESS position is format-specific - we target the formats most
/// likely to carry it (VID-HOSP, RSF-ACE-PSY, RPSA, RAA).
/// </summary>
public sealed class FinessCheck : IFileCheck
{
    private static readonly Encoding Latin1 = Encoding.GetEncoding("ISO-8859-1");
    private static readonly Regex NineDigits = new("^[0-9]{9}$", RegexOptions.Compiled);

    /// <summary>A FINESS code is 9 digits, at the head of the ATIH record.</summary>
    private const int FinessLength = 9;

    public string Name => "FINESS format";

    public IReadOnlySet<string>? AppliesTo { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "VID-HOSP", "ANO-HOSP", "RPSA", "R3A", "RSF-ACE-PSY",
    };

    static FinessCheck()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public IEnumerable<CheckFinding> Validate(string filePath, string formatName)
    {
        if (!AtihMatrix.All.ContainsKey(formatName))
        {
            yield break;
        }

        StreamReader? reader = null;
        string? readFailure = null;
        try { reader = new StreamReader(filePath, Latin1); }
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
                // FINESS is conventionally the first 9 characters of ATIH records.
                if (line.Length < FinessLength)
                {
                    continue;
                }
                var finess = line[..FinessLength].Trim();
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
                        $"N° FINESS invalide ({FindingRedaction.Position(0, FinessLength)}) : "
                        + $"{FindingRedaction.DigitGap(FinessLength, finess)}.",
                        filePath, lineNo, formatName,
                        FixHint: "Vérifier le paramétrage FINESS dans le logiciel métier.");
                }
            }
        }
    }
}
