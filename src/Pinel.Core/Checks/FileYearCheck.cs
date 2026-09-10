using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Pinel.Core.Formats;

namespace Pinel.Core.Checks;

/// <summary>
/// Checks that the DDN values inside an ATIH file are consistent with
/// the year carried in the file name (e.g. <c>FV94_RPS_2024.txt</c>).
/// This catches two classic mistakes :
/// <list type="bullet">
///   <item>Files renamed after the fact with the wrong year - DDN fall
///     outside the billing period, DRUIDES flags the chaînage.</item>
///   <item>SIH exports that concatenate multiple years into a single file,
///     then get submitted with the wrong year in the filename.</item>
/// </list>
/// </summary>
public sealed class FileYearCheck : IFileCheck
{
    private static readonly Encoding Latin1 = Encoding.GetEncoding("ISO-8859-1");
    private static readonly Regex YearInName = new(@"(?<!\d)(19|20)\d{2}(?!\d)", RegexOptions.Compiled);

    public string Name => "Cohérence année fichier";

    public IReadOnlySet<string>? AppliesTo => null;

    static FileYearCheck()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public IEnumerable<CheckFinding> Validate(string filePath, string formatName)
    {
        if (!AtihMatrix.All.TryGetValue(formatName, out var fmt)) yield break;

        var name = Path.GetFileName(filePath);
        var match = YearInName.Match(name);
        if (!match.Success) yield break; // no year in filename, nothing to validate

        var declaredYear = int.Parse(match.Value);
        var currentYear = DateTime.Now.Year;

        if (declaredYear > currentYear + 1)
        {
            yield return new CheckFinding(
                "WARN-FILE-YEAR-FUTURE", CheckSeverity.Warning,
                $"Nom de fichier contient une année future ({declaredYear}).",
                filePath, 0, formatName,
                FixHint: "Vérifier le renommage - année de cut PMSI attendue.");
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
            int futureDdn = 0;
            int tooOld = 0;
            int totalSampled = 0;
            const int sampleCap = 2000; // enough to spot a systemic mismatch
            while (reader.ReadLine() is { } line && totalSampled < sampleCap)
            {
                lineNo++;
                if (line.Length < fmt.DdnEnd) continue;

                var raw = line[fmt.DdnStart..fmt.DdnEnd].Trim();
                if (raw.Length != 8 || !raw.All(char.IsDigit) || raw.All(c => c == '0')) continue;

                totalSampled++;
                if (!DateTime.TryParseExact(raw, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var ddn))
                {
                    continue;
                }

                if (ddn.Year > declaredYear)
                {
                    futureDdn++;
                }
                else if (ddn.Year < 1900)
                {
                    tooOld++;
                }
            }

            if (futureDdn > 0)
            {
                yield return new CheckFinding(
                    "WARN-DDN-APRES-ANNEE-FICHIER", CheckSeverity.Warning,
                    $"{futureDdn} DDN postérieures à l'année déclarée {declaredYear} dans le nom du fichier.",
                    filePath, 0, formatName,
                    FixHint: "Vérifier renommage fichier ou concaténation de plusieurs années.");
            }
            if (tooOld > 0)
            {
                yield return new CheckFinding(
                    "WARN-DDN-ANNEE-ABERRANTE", CheckSeverity.Warning,
                    $"{tooOld} DDN antérieures à 1900 - parsing probablement corrompu.",
                    filePath, 0, formatName,
                    FixHint: "Vérifier encodage/format DDN (latin-1 attendu, pas utf-16).");
            }
        }
    }
}
