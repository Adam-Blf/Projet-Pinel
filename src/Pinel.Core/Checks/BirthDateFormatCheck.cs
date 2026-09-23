using System.Text;
using Pinel.Core.Formats;

namespace Pinel.Core.Checks;

/// <summary>
/// Verifie que la date de naissance est une date valide sur 8 chiffres au
/// format JJMMAAAA, dans une plage plausible (1900-2099).
/// </summary>
/// <remarks>
/// Tous les descriptifs ATIH 2026 qui portent une date de naissance (RPS, RAA,
/// VID-HOSP, VID-IPP, VID-CHAINAGE, RSF) la declarent en JJMMAAAA. Jusqu'au
/// 22/09/2026, ce controle tenait l'inverse (AAAAMMJJ) et signalait comme
/// fautive chaque ligne d'un lot pourtant accepte par e-PMSI. L'inversion
/// AAAAMMJJ reste reperee, en avertissement.
/// </remarks>
public sealed class BirthDateFormatCheck : IFileCheck
{

    /// <summary>L'ATIH code la date de naissance sur 8 chiffres, JJMMAAAA.</summary>
    private const int DdnDigits = 8;

    public string Name => "DDN JJMMAAAA";

    public IReadOnlySet<string>? AppliesTo => null; // every format has a DDN


    public IEnumerable<CheckFinding> Validate(string filePath, string formatName)
    {
        if (!AtihMatrix.All.TryGetValue(formatName, out var fmt))
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
                // Message carries the GAP, never the date of birth: findings reach
                // the UI and the JSON report on disk. See FindingRedaction.
                yield return new CheckFinding(
                    issue.Value.code,
                    issue.Value.severity,
                    $"{issue.Value.message} ({FindingRedaction.Position(fmt.DdnStart, fmt.DdnLength)}) : "
                    + $"{issue.Value.gap}.",
                    filePath, lineNo, formatName,
                    FixHint: issue.Value.fixHint);

                // Stop flooding after 50 consecutive bad DDN - it's a format issue, not a data issue.
                if (consecutiveBad >= 50) yield break;
            }
        }
    }

    /// <summary>
    /// Rend null si la date est acceptable, sinon l'anomalie. <c>gap</c> decrit
    /// l'ecart sans reproduire la date, qui est une donnee patient : l'appelant
    /// l'ajoute tel quel au message.
    /// </summary>
    private static (string code, CheckSeverity severity, string message, string gap, string fixHint)? ClassifyDdn(string ddn)
    {
        if (ddn.Length != DdnDigits || !ddn.All(char.IsDigit))
        {
            return ("ERR-DDN-NON-NUMERIC", CheckSeverity.Error,
                "DDN non numérique ou de longueur incorrecte",
                FindingRedaction.DigitGap(DdnDigits, ddn),
                "Attendu : 8 chiffres au format JJMMAAAA.");
        }

        if (IsDate(int.Parse(ddn[4..]), int.Parse(ddn[2..4]), int.Parse(ddn[..2])))
        {
            return null;
        }

        // Inversion classique : l'annee en tete (AAAAMMJJ).
        if (IsDate(int.Parse(ddn[..4]), int.Parse(ddn[4..6]), int.Parse(ddn[6..])))
        {
            return ("WARN-DDN-AAAAMMJJ", CheckSeverity.Warning,
                "DDN au format AAAAMMJJ au lieu de JJMMAAAA (ATIH)",
                "les 4 premiers chiffres forment une année plausible, les 4 derniers non",
                "Réencoder la DDN en JJMMAAAA avant envoi DRUIDES / e-PMSI.");
        }

        var year = int.Parse(ddn[4..]);
        if (year is < 1900 or > 2099)
        {
            return ("ERR-DDN-YEAR", CheckSeverity.Error,
                "Année de naissance hors plage",
                "millésime attendu entre 1900 et 2099, valeur lue en dehors",
                "Vérifier la saisie du patient dans le SIH.");
        }

        return ("ERR-DDN-DAY", CheckSeverity.Error,
            "Jour ou mois invalide dans la DDN",
            "le quantième lu n'existe pas dans le mois lu",
            "Corriger la date dans le logiciel source.");
    }

    private static bool IsDate(int year, int month, int day) =>
        year is >= 1900 and <= 2099 && month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month);
}
