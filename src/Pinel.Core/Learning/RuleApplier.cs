using Pinel.Core.Formats;

namespace Pinel.Core.Learning;

/// <summary>Effet d'une regle sur un fichier.</summary>
public sealed record RuleHit(LearnedRule Rule, string RelativePath, int Lines);

/// <summary>
/// Applique les regles apprises a un lot : decompte des lignes concernees et,
/// sur demande, copie corrigee.
/// </summary>
/// <remarks>
/// <para>
/// Une regle n'est suggeree que si elle est validee par le DIM, ou si elle est
/// proposee avec assez de recul (<see cref="SuggestionSupport"/> cas et
/// <see cref="SuggestionConfidence"/> de confiance). Seules les regles
/// validees modifient une copie : une regle apprise n'ecrit jamais seule dans
/// un fichier transmis a l'ATIH.
/// </para>
/// <para>
/// Le fichier d'origine n'est jamais modifie. La copie garde la largeur de
/// chaque champ : la nouvelle valeur est cadree a gauche et completee
/// d'espaces, comme dans les descriptifs ATIH.
/// </para>
/// </remarks>
public sealed class RuleApplier
{
    public const int SuggestionSupport = 10;
    public const double SuggestionConfidence = 0.9;

    private readonly IReadOnlyList<LearnedRule> _rules;
    private readonly ContentFormatDetector _detector;

    public RuleApplier(IEnumerable<LearnedRule> rules, IReadOnlyList<RecordSpec> specs)
    {
        _rules = rules.Where(IsSuggested).ToList();
        _detector = new ContentFormatDetector(specs);
    }

    public static bool IsSuggested(LearnedRule rule) =>
        rule.Status == RuleStatus.Validee
        || rule.Status == RuleStatus.Proposee && rule.Support >= SuggestionSupport && rule.Confidence >= SuggestionConfidence;

    /// <summary>
    /// Parcourt un dossier. Si <paramref name="outputRoot"/> est fourni, y ecrit
    /// une copie de chaque fichier touche par une regle validee.
    /// </summary>
    public IReadOnlyList<RuleHit> Run(string root, string? outputRoot = null)
    {
        var hits = new List<RuleHit>();
        foreach (var path in Directory.EnumerateFiles(root, "*.txt", SearchOption.AllDirectories).OrderBy(p => p))
        {
            var spec = _detector.Detect(path).Spec;
            if (spec is null) continue;
            var rules = _rules.Where(r => r.Format == spec.Format).ToList();
            if (rules.Count == 0) continue;

            var fields = spec.FixedFields.ToDictionary(f => f.Name, StringComparer.Ordinal);
            var counts = rules.ToDictionary(r => r, _ => 0);
            var output = new List<string>();
            bool rewritten = false;

            foreach (var line in File.ReadLines(path, PmsiEncoding.Latin1))
            {
                string? Read(string name) => fields.TryGetValue(name, out var f) ? f.Read(line) : null;
                var current = line;
                foreach (var rule in rules.Where(r => r.Matches(Read)))
                {
                    counts[rule]++;
                    if (rule.Status == RuleStatus.Validee && fields.TryGetValue(rule.Field, out var field))
                    {
                        current = Write(current, field, rule.To);
                        rewritten = true;
                    }
                }
                output.Add(current);
            }

            var relative = Path.GetRelativePath(root, path);
            hits.AddRange(counts.Where(c => c.Value > 0).Select(c => new RuleHit(c.Key, relative, c.Value)));

            if (outputRoot is not null && rewritten)
            {
                var target = Path.Combine(outputRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllLines(target, output, PmsiEncoding.Latin1);
            }
        }
        return hits;
    }

    internal static string Write(string line, FormatField field, string value)
    {
        if (line.Length < field.End) line = line.PadRight(field.End);
        var padded = value.Length >= field.Length ? value[..field.Length] : value.PadRight(field.Length);
        return string.Concat(line.AsSpan(0, field.Offset), padded, line.AsSpan(field.End));
    }
}
