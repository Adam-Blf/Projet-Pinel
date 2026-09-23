using Pinel.Core.Anonymization;
using Pinel.Core.Formats;

namespace Pinel.Core.Learning;

/// <summary>Bilan de la comparaison d'un fichier d'origine a sa version corrigee.</summary>
public sealed record PairMining(int AlignedLines, int ChangedLines, int DeletedLines, int AddedLines, IReadOnlyList<LearnedRule> Rules);

/// <summary>
/// Correction vue assez souvent pour etre remarquee, mais que le DIM n'applique
/// pas systematiquement : elle depend d'un jugement, pas d'une regle. Exemple
/// releve sur les lots 2026 : le type d'unite laisse a 000 est renseigne pour
/// quelques unites par mois, jamais pour les 160 autres.
/// </summary>
public sealed record UnexplainedCorrection(string Format, string Field, string From, string To, int Applied, int Skipped);

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
/// Une correction devient candidate des <see cref="CandidateSupport"/> cas dans
/// une paire, si le DIM l'a appliquee dans au moins <see cref="MinimumConfidence"/>
/// des cas ou elle etait possible. Le seuil de candidature est bas expres : une
/// correction rare mais reguliere (quatre unites medicales retypees chaque mois)
/// n'atteint jamais cinq cas dans un seul mois et ne serait jamais apprise. C'est
/// l'accumulation dans la memoire des regles, puis le seuil de suggestion, qui
/// demandent de la preuve avant qu'une regle ne soit proposee au DIM. Si elle ne
/// l'est pas en general, le mineur cherche un autre champ qui la conditionne
/// (« 31 devient 33 quand l'UM vaut 5420 »).
/// </para>
/// </remarks>
public static class CorrectionMiner
{
    /// <summary>Cas qu'il faut dans UNE paire pour qu'une correction devienne candidate.</summary>
    public const int CandidateSupport = 2;

    /// <summary>
    /// Au-dela de cette part de champs modifies, la ligne n'a pas ete corrigee
    /// champ par champ : elle a ete remise en forme. Une ligne decalee d'un
    /// caractere voit tous ses champs changer d'un coup et produirait autant de
    /// fausses regles qu'elle compte de champs.
    /// </summary>
    public const double RestructuredLineRatio = 0.25;

    /// <summary>Cas cumules a partir desquels une regle est montree comme etablie.</summary>
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
        var batch = MineBatch(new[] { (spec, original, corrected) }, seen, known, evidence);
        var stats = batch.PerPair[0];
        return stats with { Rules = batch.Rules };
    }

    /// <summary>
    /// Apprend sur TOUTES les paires d'un lot a la fois. L'induction est faite
    /// format par format, sur l'ensemble des lignes appariees : une correction
    /// qui ne touche qu'une ligne par fichier (retyper une unite medicale) ne
    /// serait jamais vue paire par paire, alors qu'elle se repete tous les mois.
    /// </summary>
    public static (IReadOnlyList<LearnedRule> Rules, IReadOnlyList<PairMining> PerPair, IReadOnlyList<UnexplainedCorrection> Unexplained) MineBatch(
        IEnumerable<(RecordSpec Spec, IReadOnlyList<string> Original, IReadOnlyList<string> Corrected)> pairs,
        DateOnly seen, IEnumerable<LearnedRule>? known = null, ISet<string>? evidence = null)
    {
        var perPair = new List<PairMining>();
        var byFormat = new Dictionary<string, (List<string> Fields, List<Decoded> Pairs)>(StringComparer.Ordinal);

        foreach (var (spec, original, corrected) in pairs)
        {
            var fields = LearnableFields(spec);
            var aligned = Align(spec, original, corrected, out int deleted, out int added);
            if (evidence is not null)
            {
                aligned = aligned.Where(p => evidence.Add(Fingerprint(p.Before, p.After))).ToList();
            }
            int changed = aligned.Count(p => p.Before != p.After);
            perPair.Add(new PairMining(aligned.Count, changed, deleted, added, Array.Empty<LearnedRule>()));

            if (!byFormat.TryGetValue(spec.Format, out var bucket))
            {
                byFormat[spec.Format] = bucket = (fields.Select(f => f.Name).ToList(), new List<Decoded>());
            }
            var decodedPairs = aligned
                .Select(p => new Decoded(perPair.Count - 1, Read(p.Before, fields), Read(p.After, fields)))
                .Where(d => Changes(d, bucket.Fields) <= Math.Max(2, (int)(fields.Count * RestructuredLineRatio)))
                .ToList();
            bucket.Pairs.AddRange(decodedPairs);
        }

        // Deux lectures complementaires. Paire par paire d'abord : c'est la que
        // se voit une correction massive faite en une fois (le DIM repasse tout
        // un VID-HOSP en non facturable). Puis une passe sur toutes les paires
        // d'un format, reservee aux corrections qui n'atteignent jamais le seuil
        // dans un seul fichier (retyper une unite medicale, une ligne par mois).
        // L'inverse ne marche pas : les essais successifs d'un meme mois gardent
        // des fichiers deja corriges, dont les lignes intactes compteraient
        // comme des contre-exemples et effaceraient la correction massive.
        var rules = new List<LearnedRule>();
        foreach (var (format, (fields, decoded)) in byFormat)
        {
            var merged = new Dictionary<string, LearnedRule>(StringComparer.Ordinal);
            foreach (var slice in decoded.GroupBy(d => d.Pair))
            {
                foreach (var rule in Induce(format, fields, slice.ToList(), seen))
                {
                    if (merged.TryGetValue(rule.Key, out var seenRule))
                    {
                        seenRule.Support += rule.Support;
                        seenRule.Contradictions += rule.Contradictions;
                    }
                    else merged[rule.Key] = rule;
                }
            }
            foreach (var rule in Induce(format, fields, decoded, seen))
            {
                merged.TryAdd(rule.Key, rule);
            }
            rules.AddRange(merged.Values);

            foreach (var rule in (known ?? Enumerable.Empty<LearnedRule>())
                         .Where(r => r.Format == format && !merged.ContainsKey(r.Key)))
            {
                var applicable = decoded.Where(p => rule.Matches(f => p.Before.GetValueOrDefault(f))).ToList();
                if (applicable.Count == 0) continue;
                rules.Add(NewRule(rule.Format, rule.Field, rule.From, rule.To, rule.ConditionField, rule.ConditionValue,
                    applicable.Count(p => p.After[rule.Field] == rule.To),
                    applicable.Count(p => p.After[rule.Field] == rule.From), seen));
            }
        }
        var keys = rules.Select(r => r.Key).ToHashSet(StringComparer.Ordinal);
        var unexplained = byFormat
            .SelectMany(entry => Unexplained(entry.Key, entry.Value.Fields, entry.Value.Pairs))
            .Where(u => !keys.Contains($"{u.Format}|{u.Field}|{u.From}|{u.To}||"))
            .OrderByDescending(u => u.Applied)
            .ToList();
        return (rules, perPair, unexplained);
    }

    /// <summary>Transitions assez frequentes pour etre vues, trop irregulieres pour devenir une regle.</summary>
    private static IEnumerable<UnexplainedCorrection> Unexplained(string format, IReadOnlyList<string> fields, IReadOnlyList<Decoded> pairs) =>
        pairs
            .SelectMany(p => fields.Where(f => p.Before[f] != p.After[f]).Select(f => (Field: f, From: p.Before[f], To: p.After[f])))
            .GroupBy(t => t)
            .Where(g => g.Count() >= CandidateSupport)
            .Select(g => new UnexplainedCorrection(format, g.Key.Field, g.Key.From, g.Key.To, g.Count(),
                pairs.Count(p => p.Before[g.Key.Field] == g.Key.From && p.After[g.Key.Field] == g.Key.From)))
            .Where(u => u.Skipped > u.Applied);

    private sealed record Decoded(int Pair, Dictionary<string, string> Before, Dictionary<string, string> After);

    private static int Changes(Decoded pair, IEnumerable<string> fields) =>
        fields.Count(f => pair.Before[f] != pair.After[f]);

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
        IReadOnlyList<Decoded> pairs, DateOnly seen)
    {
        var rules = new List<LearnedRule>();
        var transitions = pairs
            .SelectMany(p => fieldNames.Where(f => p.Before[f] != p.After[f]).Select(f => (Field: f, From: p.Before[f], To: p.After[f])))
            .GroupBy(t => t)
            .Where(g => g.Count() >= CandidateSupport);

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
                    .Where(v => v.Support >= CandidateSupport && v.Support / (double)(v.Support + v.Kept) >= MinimumConfidence)
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
