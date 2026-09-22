using System.Text.RegularExpressions;
using Pinel.Core.Formats;

namespace Pinel.Core.Learning;

/// <summary>Un fichier d'origine et sa version corrigee par le DIM, de meme format.</summary>
public sealed record CorrectionPair(string Original, string Corrected, RecordSpec Spec);

/// <summary>
/// Retrouve dans l'arborescence de travail du DIM les fichiers corriges a la
/// main et le fichier d'origine de chacun.
/// </summary>
/// <remarks>
/// <para>
/// Les conventions relevees sur les lots 2026 : suffixe <c>_CORR_main</c>,
/// <c>_CORRIGE_main</c>, <c>_corrige_main</c>, <c> - Corriger</c> ; l'origine
/// est rangee a cote, dans un sous-dossier <c>fic_avant_corr</c>, ou dans un
/// essai precedent du meme mois (<c>M 2\essai_01</c> pour <c>M 2\essai_03</c>).
/// </para>
/// <para>
/// L'origine retenue est d'abord celle qui est rangee a cote du fichier
/// corrige ; a defaut, parmi les candidats de meme format du mois, celle qui
/// partage le plus de lignes avec lui.
/// </para>
/// </remarks>
public sealed class CorrectionPairFinder
{
    private static readonly Regex CorrectionMark = new(
        @"([\s_\-]*-\s*corriger|[\s_\-]*corr(ig(e|é)|ection)?[\s_\-]*main[\s_\-]*|[\s_\-]*corrig(e|é)|[\s_\-]*corr\b)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex MonthFolder = new(@"^M\s*\d{1,2}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ContentFormatDetector _detector;

    public CorrectionPairFinder(IReadOnlyList<RecordSpec> specs) => _detector = new ContentFormatDetector(specs);

    public static bool IsCorrected(string path) => CorrectionMark.IsMatch(Path.GetFileNameWithoutExtension(path));

    public IReadOnlyList<CorrectionPair> Find(string root)
    {
        var pairs = new List<CorrectionPair>();
        var texts = Directory.EnumerateFiles(root, "*.txt", SearchOption.AllDirectories).ToList();
        var formats = new Dictionary<string, RecordSpec?>(StringComparer.OrdinalIgnoreCase);
        RecordSpec? FormatOf(string path) =>
            formats.TryGetValue(path, out var spec) ? spec : formats[path] = _detector.Detect(path).Spec;

        foreach (var corrected in texts.Where(IsCorrected))
        {
            var spec = FormatOf(corrected);
            if (spec is null) continue;

            var stem = Stem(corrected);
            var month = MonthOf(corrected);
            var candidates = texts
                .Where(p => p != corrected && !IsCorrected(p))
                .Where(p => MonthOf(p) == month)
                .Where(p => Stem(p).StartsWith(stem, StringComparison.OrdinalIgnoreCase)
                            || stem.StartsWith(Stem(p), StringComparison.OrdinalIgnoreCase))
                .Where(p => FormatOf(p)?.Format == spec.Format)
                .ToList();
            if (candidates.Count == 0) continue;

            // L'origine la plus proche d'abord : meme dossier ou fic_avant_corr,
            // puis les autres essais du mois. Un export plus ancien peut partager
            // davantage de lignes avec le corrige sans etre celui que le DIM a
            // corrige (changement de parametrage Druides en cours de mois).
            var correctedLines = File.ReadLines(corrected, PmsiEncoding.Latin1).ToHashSet(StringComparer.Ordinal);
            var best = candidates
                .Select(p => (Path: p, Near: Proximity(p, corrected),
                              Shared: File.ReadLines(p, PmsiEncoding.Latin1).Count(correctedLines.Contains)))
                // Voisin immediat : le meme format suffit, une correction
                // systematique (VID-HOSP : 1 003 lignes sur 1 153) ne laisse
                // presque aucune ligne intacte. Ailleurs dans le mois, la
                // majorite des lignes doit etre partagee.
                .Where(c => c.Near == 0 || c.Shared * 2 >= correctedLines.Count)
                .OrderBy(c => c.Near)
                .ThenByDescending(c => c.Shared)
                .FirstOrDefault();
            if (best.Path is not null) pairs.Add(new CorrectionPair(best.Path, corrected, spec));
        }

        return pairs;
    }

    /// <summary>0 : meme dossier ou fic_avant_corr ; 1 : dossier parent ; 2 : ailleurs dans le mois.</summary>
    private static int Proximity(string candidate, string corrected)
    {
        var folder = Path.GetDirectoryName(corrected)!;
        var candidateFolder = Path.GetDirectoryName(candidate)!;
        if (string.Equals(candidateFolder, folder, StringComparison.OrdinalIgnoreCase)
            || string.Equals(Path.GetDirectoryName(candidateFolder), folder, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }
        return string.Equals(candidateFolder, Path.GetDirectoryName(folder), StringComparison.OrdinalIgnoreCase) ? 1 : 2;
    }

    /// <summary>Nom sans marque de correction, sans horodatage ni chiffres de fin.</summary>
    internal static string Stem(string path)
    {
        var name = CorrectionMark.Replace(Path.GetFileNameWithoutExtension(path), "");
        name = Regex.Replace(name, @"[\s_\-]*\d{6,}.*$", "");
        return name.Trim(' ', '_', '-');
    }

    private static string MonthOf(string path)
    {
        for (var dir = new DirectoryInfo(Path.GetDirectoryName(path)!); dir is not null; dir = dir.Parent)
        {
            if (MonthFolder.IsMatch(dir.Name)) return dir.FullName;
        }
        return Path.GetDirectoryName(path)!;
    }
}
