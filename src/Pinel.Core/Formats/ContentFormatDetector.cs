namespace Pinel.Core.Formats;

/// <summary>Resultat de la reconnaissance d'un fichier par son contenu.</summary>
public sealed record DetectionResult(RecordSpec? Spec, double Ratio, int LinesRead, string Reason);

/// <summary>
/// Reconnait le format d'un fichier a largeur fixe d'apres la longueur de ses
/// lignes, et non plus d'apres son nom.
/// </summary>
/// <remarks>
/// <para>
/// Les exports reels portent des noms que l'identification par le nom ne sait
/// pas lire (vh_psy, vipp, fc_ic, fc_htpart, aa.txt) ou qu'elle lit de travers
/// (dim_rps et PSY_RAA_HOSP_PMSI reconnus comme RPS et RAA). La longueur d'une
/// ligne, rapportee a ses propres compteurs de zones repetees, est une
/// signature bien plus sure.
/// </para>
/// <para>
/// Un format est retenu quand au moins 80 % des lignes lues ont exactement la
/// longueur attendue. A egalite, le nom du fichier puis le domaine (dossier PSY
/// ou MCO) departagent. Un fichier qui reste ambigu n'est pas reconnu : mieux
/// vaut l'ecarter que le decouper avec le mauvais descriptif.
/// </para>
/// </remarks>
public sealed class ContentFormatDetector
{
    private const int SampleLines = 3000;
    private const double Threshold = 0.8;
    private readonly IReadOnlyList<RecordSpec> _specs;

    public ContentFormatDetector(IReadOnlyList<RecordSpec> specs) => _specs = specs;

    public DetectionResult Detect(string path)
    {
        var lines = new List<string>(SampleLines);
        foreach (var line in File.ReadLines(path, PmsiEncoding.Latin1))
        {
            if (line.Length == 0) continue;
            lines.Add(line);
            if (lines.Count == SampleLines) break;
        }
        if (lines.Count == 0) return new DetectionResult(null, 0, 0, "fichier vide");

        var scored = _specs
            .Select(spec => (Spec: spec, Ratio: lines.Count(l => l.Length == spec.ExpectedLength(l)) / (double)lines.Count))
            .Where(s => s.Ratio >= Threshold)
            .OrderByDescending(s => s.Ratio)
            .ToList();

        if (scored.Count == 0)
        {
            return new DetectionResult(null, 0, lines.Count, "aucun format ne correspond a la longueur des lignes");
        }

        double best = scored[0].Ratio;
        var top = scored.Where(s => best - s.Ratio < 0.02).Select(s => s.Spec).ToList();
        if (top.Count == 1) return new DetectionResult(top[0], best, lines.Count, "longueur des lignes");

        var byName = AtihFormatIdentifier.Identify(path);
        var named = top.Where(s => string.Equals(s.Format, byName, StringComparison.OrdinalIgnoreCase)).ToList();
        if (named.Count == 1) return new DetectionResult(named[0], best, lines.Count, "longueur des lignes et nom du fichier");

        var domain = DomainOf(path);
        if (domain is not null)
        {
            // Un format suffixe du domaine du dossier (FICUM-MCO sous MCO\)
            // l'emporte, puis un format sans domaine face a un format d'un
            // autre domaine.
            var suffixed = top.Where(s => DomainOf(s.Format) == domain).ToList();
            if (suffixed.Count == 1) return new DetectionResult(suffixed[0], best, lines.Count, "longueur des lignes et domaine");
            var compatible = top.Where(s => DomainOf(s.Format) is null).ToList();
            if (suffixed.Count == 0 && compatible.Count == 1)
            {
                return new DetectionResult(compatible[0], best, lines.Count, "longueur des lignes et domaine");
            }
        }

        return new DetectionResult(null, best, lines.Count,
            "ambigu entre " + string.Join(", ", top.Select(s => s.Format)));
    }

    private static string? DomainOf(string text)
    {
        var upper = text.ToUpperInvariant().Replace('/', '\\');
        if (upper.Contains("\\PSY\\") || upper.EndsWith("-PSY")) return "PSY";
        if (upper.Contains("\\MCO\\") || upper.EndsWith("-MCO")) return "MCO";
        return null;
    }
}
