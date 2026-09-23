using System.Text;
using Pinel.Core.Formats;

namespace Pinel.Core.Checks;

/// <summary>
/// Anonymized ATIH files (RPSA, R3A, RAPSS, RAPSS-HAD, SSRHA, SSRHA-HAD)
/// must NOT contain clear-text IPP. Any IPP that looks human-readable
/// (letters, dashes) in an anonymized file is a potential CNIL / GDPR
/// incident: the nominal identifier leaked into the anonymized stream.
/// The findings themselves are redacted through <see cref="FindingRedaction"/>,
/// otherwise reporting the leak would repeat it.
/// </summary>
public sealed class AnonymizationCheck : IFileCheck
{

    public string Name => "Anonymisation RPSA/R3A";

    /// <summary>
    /// Only RPSA and R3A. The other anonymised formats once listed here
    /// (RAPSS, RAPSS-HAD, SSRHA, SSRHA-HAD) carry no patient field at all in
    /// the official 2026 descriptors, so there is nothing for this check to
    /// look at and it would only produce noise.
    /// </summary>
    public IReadOnlySet<string>? AppliesTo { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "RPSA", "R3A",
    };

    /// <summary>
    /// Window of the "cryptage irreversible de l'IPP" field, positions 25 to 40
    /// in the official ATIH descriptors (workbook "formats_anonymes_psy_2026",
    /// sheets RPSA and R3A), expressed here as a zero-indexed half-open range.
    ///
    /// This window is deliberately NOT taken from <see cref="AtihMatrix"/>:
    /// those formats declare no patient identifier precisely so that no parser
    /// extracts anything from them. The check needs the position to verify that
    /// the anonymisation actually happened, which is the opposite intent, so it
    /// carries its own constant with its source written next to it.
    /// </summary>
    private const int HashStart = 24;
    private const int HashEnd = 40;


    public IEnumerable<CheckFinding> Validate(string filePath, string formatName)
    {
        if (!AtihMatrix.All.ContainsKey(formatName)) yield break;

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
                if (line.Length < HashEnd) continue;
                var ipp = line[HashStart..HashEnd].Trim();
                if (ipp.Length == 0) continue;

                // Message carries the GAP, never the IPP: findings reach the UI
                // and the JSON report on disk. See FindingRedaction.
                var position = FindingRedaction.Position(HashStart, HashEnd - HashStart);
                var letters = ipp.Count(char.IsLetter);
                if (letters > 0)
                {
                    yield return new CheckFinding(
                        "ERR-ANONYM-LETTERS", CheckSeverity.Error,
                        $"IPP alphabétique dans un fichier anonymisé ({position}) : "
                        + $"{FindingRedaction.CharacterGap(letters, "lettre", ipp.Length)}.",
                        filePath, lineNo, formatName,
                        FixHint: "Régénérer l'export anonyme depuis le logiciel de groupage (PIVOINE / DRUIDES).");
                }
                else if (ipp.Contains('-'))
                {
                    yield return new CheckFinding(
                        "WARN-ANONYM-DASH", CheckSeverity.Warning,
                        $"IPP ponctué d'un tiret dans un fichier anonymisé ({position}) : "
                        + $"{FindingRedaction.CharacterGap(ipp.Count(c => c == '-'), "tiret", ipp.Length)}.",
                        filePath, lineNo, formatName,
                        FixHint: "Vérifier que le fichier est bien issu du module anonymisation.");
                }
            }
        }
    }
}
