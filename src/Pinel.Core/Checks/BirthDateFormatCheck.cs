using System.Text;
using Pinel.Core.Formats;

namespace Pinel.Core.Checks;

/// <summary>
/// Verifies that the DDN (Date De Naissance) field is an 8-digit
/// YYYYMMDD string within a plausible range (1900-2099) and represents
/// a valid calendar date. The most common DRUIDES rejection on PSY
/// files is a DDN emitted as DDMMYYYY instead of YYYYMMDD - this
/// detector catches that before upload.
/// </summary>
public sealed class BirthDateFormatCheck : IFileCheck
{
    private static readonly Encoding Latin1 = Encoding.GetEncoding("ISO-8859-1");

    public string Name => "DDN YYYYMMDD";

    public IReadOnlySet<string>? AppliesTo => null; // every format has a DDN

    static BirthDateFormatCheck()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public IEnumerable<CheckFinding> Validate(string filePath, string formatName)
    {
        if (!AtihMatrix.All.TryGetValue(formatName, out var fmt))
        {
            yield break;
        }

        StreamReader? reader = null;
        string? ioError = null;
        try { reader = new StreamReader(filePath, Latin1); }
        catch (IOException ex) { ioError = ex.Message; }

        if (reader is null)
        {
            yield return new CheckFinding(
                "ERR-IO", CheckSeverity.Blocker,
                $"Impossible de lire le fichier : {ioError}",
                filePath, 0, formatName);
            yield break;
        }

        using (reader)
        {
            int lineNo = 0;
            int consecutiveBad = 0;
            while (reader.ReadLine() is { } rawLine)
            {
                lineNo++;
                var line = rawLine.Length >= fmt.Length ? rawLine[..fmt.Length] : rawLine.PadRight(fmt.Length);
                if (line.Length < fmt.DdnEnd) continue;

                var ddn = line[fmt.DdnStart..fmt.DdnEnd].Trim();
                if (ddn.Length == 0 || ddn.All(c => c == '0')) continue;

                var issue = ClassifyDdn(ddn);
                if (issue is null)
                {
                    consecutiveBad = 0;
                    continue;
                }

                consecutiveBad++;
                yield return new CheckFinding(
                    issue.Value.code,
                    issue.Value.severity,
                    issue.Value.message + $" Valeur lue : « {ddn} ».",
                    filePath, lineNo, formatName,
                    FixHint: issue.Value.fixHint);

                // Stop flooding after 50 consecutive bad DDN - it's a format issue, not a data issue.
                if (consecutiveBad >= 50) yield break;
            }
        }
    }

    /// <summary>
    /// Returns null if the DDN is acceptable, otherwise a tuple describing
    /// the anomaly. The heuristic privileges ATIH convention (YYYYMMDD) and
    /// flags the classic DDMMYYYY misencoding as a warning, not a blocker,
    /// because some legacy exports at Fondation Vallée used that layout in 2021.
    /// </summary>
    private static (string code, CheckSeverity severity, string message, string fixHint)? ClassifyDdn(string ddn)
    {
        if (ddn.Length != 8 || !ddn.All(char.IsDigit))
        {
            return ("ERR-DDN-NON-NUMERIC", CheckSeverity.Error,
                "DDN non numérique ou de longueur incorrecte.",
                "Attendu : 8 chiffres au format YYYYMMDD.");
        }

        var year = int.Parse(ddn[..4]);
        var month = int.Parse(ddn[4..6]);
        var day = int.Parse(ddn[6..8]);

        if (year is >= 1900 and <= 2099 && month is >= 1 and <= 12 && day is >= 1 and <= 31)
        {
            try
            {
                _ = new DateOnly(year, month, day);
                return null; // valid
            }
            catch (ArgumentOutOfRangeException)
            {
                return ("ERR-DDN-DAY", CheckSeverity.Error,
                    "Jour/mois invalide dans la DDN.",
                    "Corriger la date dans le logiciel source.");
            }
        }

        // Try DDMMYYYY: last 4 could be the year.
        var tailYear = int.Parse(ddn[4..]);
        if (tailYear is >= 1900 and <= 2099)
        {
            return ("WARN-DDN-DDMMYYYY", CheckSeverity.Warning,
                "DDN semble au format DDMMYYYY au lieu de YYYYMMDD (ATIH).",
                "Réencoder la DDN en YYYYMMDD avant envoi DRUIDES / e-PMSI.");
        }

        return ("ERR-DDN-YEAR", CheckSeverity.Error,
            "Année de naissance hors de la plage 1900-2099.",
            "Vérifier la saisie du patient dans le SIH.");
    }
}
