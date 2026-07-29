using System.Text;
using Pinel.Core.Formats;

namespace Pinel.Core.Checks;

/// <summary>
/// Anonymized ATIH files (RPSA, R3A, RAPSS, RAPSS-HAD, SSRHA, SSRHA-HAD)
/// must NOT contain clear-text IPP. Any IPP that looks human-readable
/// (letters, dashes, embedded year, excessive non-zero prefix) in an
/// anonymized file is a potential CNIL / GDPR incident: the nominal
/// identifier leaked into the anonymized stream.
/// </summary>
public sealed class AnonymizationCheck : IFileCheck
{
    private static readonly Encoding Latin1 = Encoding.GetEncoding("ISO-8859-1");

    public string Name => "Anonymisation RPSA/R3A";

    public IReadOnlySet<string>? AppliesTo { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "RPSA", "R3A", "RAPSS", "RAPSS-HAD", "SSRHA", "SSRHA-HAD",
    };

    static AnonymizationCheck()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public IEnumerable<CheckFinding> Validate(string filePath, string formatName)
    {
        if (!AtihMatrix.All.TryGetValue(formatName, out var fmt)) yield break;

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
            while (reader.ReadLine() is { } line)
            {
                lineNo++;
                if (line.Length < fmt.IppEnd) continue;
                var ipp = line[fmt.IppStart..fmt.IppEnd].Trim();
                if (ipp.Length == 0) continue;

                if (ipp.Any(char.IsLetter))
                {
                    yield return new CheckFinding(
                        "ERR-ANONYM-LETTERS", CheckSeverity.Error,
                        $"IPP contient des lettres dans un fichier anonymisé : « {ipp} ».",
                        filePath, lineNo, formatName,
                        FixHint: "Régénérer l'export anonyme depuis le logiciel de groupage (PIVOINE / DRUIDES).");
                }
                else if (ipp.Contains('-'))
                {
                    yield return new CheckFinding(
                        "WARN-ANONYM-DASH", CheckSeverity.Warning,
                        $"IPP contient un tiret dans un fichier anonymisé : « {ipp} ».",
                        filePath, lineNo, formatName,
                        FixHint: "Vérifier que le fichier est bien issu du module anonymisation.");
                }
            }
        }
    }
}
