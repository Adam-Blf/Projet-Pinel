using Pinel.Core.Anonymization;
using Pinel.Core.Formats;

namespace Pinel.Core.Learning;

/// <summary>Bilan de la comparaison d'un fichier d'origine a sa version corrigee.</summary>
public sealed record PairMining(int AlignedLines, int ChangedLines, int DeletedLines, int AddedLines, IReadOnlyList<LearnedRule> Rules);

/// <summary>
/// Compare un fichier d'origine a sa version corrigee par le DIM et en tire
/// les corrections regulieres.
/// </summary>
/// <remarks>
/// <para>
/// Alignement : ligne a ligne quand les deux fichiers ont le meme nombre de
/// lignes (VID-HOSP, VID-IPP, FICUM), sinon par cle (identifiants et dates de
/// la ligne, qui ne sont jamais corriges en meme temps que les codes).
/// </para>
/// <para>
/// Seuls les champs de code (role <see cref="FieldRole.Keep"/>) sont appris.
/// Une correction devient une regle quand le DIM l'a appliquee au moins
/// <see cref="MinimumSupport"/> fois et dans au moins
/// <see cref="MinimumConfidence"/> des cas ou elle etait possible. Si elle ne
/// l'est pas en general, le mineur cherche un autre champ qui la conditionne
/// (« 31 devient 33 quand l'UM vaut 5420 »).
/// </para>
/// </remarks>
public static class CorrectionMiner
{
    public const int MinimumSupport = 5;
    public const double MinimumConfidence = 0.95;

    /// <param name="known">Regles deja connues : chacune est reevaluee sur la paire, pour
    /// qu'une correction que le DIM cesse d'appliquer perde sa confiance.</param>
    /// <param name="evidence">Couples de lignes (avant, apres) deja comptes dans cet
    /// apprentissage. Un meme mois garde souvent plusieurs copies de la meme
    /// correction (envois 05, 06, 07) : sans ce filtre, une correction refaite trois
    /// fois pesait trois fois.</param>
    public static PairMining Mine(RecordSpec spec, IReadOnlyList<string> original, IReadOnlyList<string> corrected,
        DateOnly seen, IEnumerable<LearnedRule>? known = null, ISet<string>? evidence = null)
    {
        var fields = LearnableFields(spec);
        var pairs = Align(spec, original, corrected, out int deleted, out int added);
        if (evidence is not null)
        {
            pairs = pairs.Where(p => evidence.Add(Fingerprint(p.Before, p.After))).ToList();
        }
        var decoded = pairs
            .Select(p => (Before: Read(p.Before, fields), After: Read(p.After, fields)))
            .ToList();
        int changed = pairs.Count(p => p.Before != p.After);
        var rules = Induce(spec.Format, fields.Select(f => f.Name).ToList(), decoded, seen);

        var induced = rules.Select(r => r.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var rule in (known ?? Enumerable.Empty<LearnedRule>())
                     .Where(r => r.Format == spec.Format && !induced.Contains(r.Key)))
        {
            var applicable = decoded.Where(p => rule.Matches(f => p.Before.GetValueOrDefault(f))).ToList();
            if (applicable.Count == 0) continue;
            rules.Add(NewRule(rule.Format, rule.Field, rule.From, rule.To, rule.ConditionField, rule.ConditionValue,
                applicable.Count(p => p.After[rule.Field] == rule.To),
                applicable.Count(p => p.After[rule.Field] == rule.From), seen));
        }
        return new PairMining(pairs.Count, changed, deleted, added, rules);
    }

    /// <summary>Champs de code du format, hors fillers.</summary>
    internal static IReadOnlyList<FormatField> LearnableFields(RecordSpec spec) =>
        spec.FixedFields
            .Where((f, i) => spec.FixedRoles[i] == FieldRole.Keep && !f.Name.StartsWith("FILLER", StringComparison.Ordinal))
            .ToList();

    private static Dictionary<string, string> Read(string line, IReadOnlyList<FormatField> fields) =>
        fields.ToDictionary(f => f.Name, f => f.Read(line), StringComparer.Ordinal);

    private static List<(string Before, string After)> Align(RecordSpec spec, IReadOnlyList<string> a, IReadOnlyList<string> b,
        out int deleted, out int added)
    {
        var result = new List<(string, string)>();
        // Meme nombre de lignes, en majorite quasi identiques au meme rang : le
        // DIM a corrige sur place, l'alignement par rang est le bon.
        if (a.Count == b.Count && a.Count > 0
            && Enumerable.Range(0, a.Count).Count(i => Distance(a[i], b[i]) <= 8) * 2 >= a.Count)
        {
            for (int i = 0; i < a.Count; i++) result.Add((a[i], b[i]));
            deleted = added = 0;
            return result;
        }

        var keyFields = spec.FixedFields
            .Where((f, i) => spec.FixedRoles[i] is FieldRole.PatientId or FieldRole.StayId or FieldRole.Nir or FieldRole.Date)
            .ToList();
        string Key(string line) => string.Join('\u001f', keyFields.Select(f => f.Read(line)));

        var pool = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var line in b)
        {
            var key = Key(line);
            if (!pool.TryGetValue(key, out var list)) pool[key] = list = new List<string>();
            list.Add(line);
        }

        deleted = 0;
        foreach (var line in a)
        {
            if (!pool.TryGetValue(Key(line), out var candidates) || candidates.Count == 0)
            {
                deleted++;
                continue;
            }
            // Candidat le plus proche : identique d'abord, sinon le moins different.
            int best = 0, bestDistance = int.MaxValue;
            for (int i = 0; i < candidates.Count && bestDistance > 0; i++)
            {
                int distance = Distance(line, candidates[i]);
                if (distance < bestDistance) { best = i; bestDistance = distance; }
            }
            result.Add((line, candidates[best]));
            candidates.RemoveAt(best);
        }
        added = pool.Values.Sum(l => l.Count);
        return result;
    }

    private static string Fingerprint(string before, string after) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.Latin1.GetBytes(before + "\n" + after)).AsSpan(0, 12));

    private static int Distance(string x, string y)
    {
        int d = Math.Abs(x.Length - y.Length);
        for (int i = 0; i < Math.Min(x.Length, y.Length); i++) if (x[i] != y[i]) d++;
        return d;
    }

    private static List<LearnedRule> Induce(string format, IReadOnlyList<string> fieldNames,
        IReadOnlyList<(Dictionary<string, string> Before, Dictionary<string, string> After)> pairs, DateOnly seen)
    {
        var rules = new List<LearnedRule>();
        var transitions = pairs
            .SelectMany(p => fieldNames.Where(f => p.Before[f] != p.After[f]).Select(f => (Field: f, From: p.Before[f], To: p.After[f])))
            .GroupBy(t => t)
            .Where(g => g.Count() >= MinimumSupport);

        foreach (var group in transitions)
        {
            var (field, from, to) = group.Key;
            var candidates = pairs.Where(p => p.Before[field] == from).ToList();
            int support = candidates.Count(p => p.After[field] == to);
            int kept = candidates.Count(p => p.After[field] == from);

            if (support / (double)(support + kept) >= MinimumConfidence)
            {
                rules.Add(NewRule(format, field, from, to, null, null, support, kept, seen));
                continue;
            }

            // Recherche d'une condition : le champ dont une valeur rend la
            // correction systematique et couvre le plus de cas.
            List<LearnedRule>? bestSet = null;
            int bestCovered = 0;
            foreach (var condition in fieldNames.Where(f => f != field))
            {
                var set = candidates
                    .GroupBy(p => p.Before[condition])
                    .Select(g => (Value: g.Key, Support: g.Count(p => p.After[field] == to), Kept: g.Count(p => p.After[field] == from)))
                    .Where(v => v.Support >= MinimumSupport && v.Support / (double)(v.Support + v.Kept) >= MinimumConfidence)
                    .Select(v => NewRule(format, field, from, to, condition, v.Value, v.Support, v.Kept, seen))
                    .ToList();
                int covered = set.Sum(r => r.Support);
                if (covered > bestCovered) { bestCovered = covered; bestSet = set; }
            }
            if (bestSet is not null) rules.AddRange(bestSet);
        }
        return rules;
    }

    private static LearnedRule NewRule(string format, string field, string from, string to, string? conditionField,
        string? conditionValue, int support, int contradictions, DateOnly seen) => new()
    {
        Format = format,
        Field = field,
        From = from,
        To = to,
        ConditionField = conditionField,
        ConditionValue = conditionValue,
        Support = support,
        Contradictions = contradictions,
        FirstSeen = seen,
        LastSeen = seen,
    };
}
