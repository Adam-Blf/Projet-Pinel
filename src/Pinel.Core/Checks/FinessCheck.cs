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
        string? ioError = null;
        try { reader = new StreamReader(filePath, Latin1); }
        catch (IOException ex) { ioError = ex.Message; }

        if (reader is null)
        {
            yield return new CheckFinding(
                "ERR-IO", CheckSeverity.Blocker,
                $"Impossible de lire le fichier : {ioError}",
                filePath, 0, formatName,
                FixHint: "Vérifier les permissions et la présence du fichier.");
            yield break;
        }

        using (reader)
        {
            int lineNo = 0;
            while (reader.ReadLine() is { } line)
            {
                lineNo++;
                // FINESS is conventionally the first 9 characters of ATIH records.
                if (line.Length < 9)
                {
                    continue;
                }
                var finess = line[..9].Trim();
                if (finess.Length == 0)
                {
                    continue; // common padding line
                }
                if (!NineDigits.IsMatch(finess))
                {
                    yield return new CheckFinding(
                        "ERR-FINESS-FORMAT", CheckSeverity.Error,
                        $"N° FINESS invalide : « {finess} ». Attendu : 9 chiffres.",
                        filePath, lineNo, formatName,
                        FixHint: "Vérifier le paramétrage FINESS dans le logiciel métier.");
                }
            }
        }
    }
}
