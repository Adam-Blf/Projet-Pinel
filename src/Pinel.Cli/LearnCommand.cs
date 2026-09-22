using Pinel.Core.Formats;
using Pinel.Core.Learning;

namespace Pinel.Cli;

/// <summary>
/// Commandes <c>apprendre</c> et <c>regles</c> : tirer les corrections
/// regulieres du DIM des paires origine / corrige d'une arborescence, et
/// relire les regles apprises.
/// </summary>
internal static class LearnCommand
{
    public static int Learn(string root, string rulesPath, string formatsDirectory)
    {
        var specs = Specs.Load(formatsDirectory);
        var store = RuleStore.Load(rulesPath);
        var pairs = new CorrectionPairFinder(specs).Find(root);
        var today = DateOnly.FromDateTime(DateTime.Today);

        Console.WriteLine($"{pairs.Count} paire(s) origine / corrige trouvee(s).");
        int learned = 0;
        var evidence = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pair in pairs)
        {
            var fingerprint = RuleStore.Fingerprint(pair.Original, pair.Corrected);
            if (store.LearnedPairs.Contains(fingerprint)) continue;

            var mining = CorrectionMiner.Mine(
                pair.Spec,
                File.ReadAllLines(pair.Original, PmsiEncoding.Latin1),
                File.ReadAllLines(pair.Corrected, PmsiEncoding.Latin1),
                today, store.Rules, evidence);
            store.Merge(fingerprint, mining.Rules);
            learned++;
            Console.WriteLine(
                $"   {pair.Spec.Format,-10} {Path.GetRelativePath(root, pair.Corrected)} : " +
                $"{mining.ChangedLines} ligne(s) modifiee(s), {mining.DeletedLines} supprimee(s), {mining.AddedLines} ajoutee(s)");
        }

        store.Save(rulesPath);
        Console.WriteLine($"{learned} paire(s) nouvelle(s) apprise(s), {store.Rules.Count} regle(s) en memoire.");
        return Show(rulesPath);
    }

    public static int Show(string rulesPath)
    {
        var store = RuleStore.Load(rulesPath);
        Console.WriteLine();
        Console.WriteLine("Regles (numero ; confiance ; cas ; statut ; * = suggeree sur les prochains lots) :");
        for (int i = 0; i < store.Rules.Count; i++)
        {
            var rule = store.Rules[i];
            var mark = RuleApplier.IsSuggested(rule) ? "*" : " ";
            Console.WriteLine($"   {i + 1,3} {rule.Confidence,5:P0} {rule.Support,6} {rule.Status,-9}{mark} {rule.Describe()}");
        }
        return 0;
    }

    /// <summary>Validation ou rejet d'une regle par le DIM, par son numero.</summary>
    public static int SetStatus(string rulesPath, string number, string decision)
    {
        var store = RuleStore.Load(rulesPath);
        if (!int.TryParse(number, out var n) || n < 1 || n > store.Rules.Count)
        {
            throw new FormatException($"Numero de regle inconnu : {number}");
        }
        store.Rules[n - 1].Status = decision.ToLowerInvariant() switch
        {
            "valider" => RuleStatus.Validee,
            "rejeter" => RuleStatus.Rejetee,
            "proposer" => RuleStatus.Proposee,
            _ => throw new FormatException("Decision attendue : valider, rejeter ou proposer."),
        };
        store.Save(rulesPath);
        Console.WriteLine($"Regle {n} : {store.Rules[n - 1].Status}. {store.Rules[n - 1].Describe()}");
        return 0;
    }

    public static int Suggest(string folder, string rulesPath, string formatsDirectory, string? output)
    {
        var store = RuleStore.Load(rulesPath);
        var hits = new RuleApplier(store.Rules, Specs.Load(formatsDirectory)).Run(folder, output);
        Console.WriteLine($"{hits.Sum(h => h.Lines)} ligne(s) concernee(s) par {hits.Select(h => h.Rule).Distinct().Count()} regle(s).");
        foreach (var group in hits.GroupBy(h => h.Rule).OrderByDescending(g => g.Sum(h => h.Lines)))
        {
            var applied = group.Key.Status == RuleStatus.Validee && output is not null ? "appliquee" : "a revoir ";
            Console.WriteLine($"   {group.Sum(h => h.Lines),7} {applied} {group.Key.Describe()}");
        }
        if (output is not null) Console.WriteLine($"Copies corrigees (regles validees seulement) : {output}");
        return 0;
    }
}

/// <summary>Chargement des descriptifs d'un dossier en structures d'enregistrement.</summary>
internal static class Specs
{
    public static IReadOnlyList<RecordSpec> Load(string formatsDirectory)
    {
        var registry = new LayoutRegistry(Array.Empty<FormatLayout>());
        if (registry.LoadDirectory(formatsDirectory) == 0)
        {
            throw new IOException($"Aucun descriptif dans {formatsDirectory}");
        }
        return registry.Formats
            .Select(f => registry.Resolve(f))
            .OfType<FormatLayout>()
            .Select(RecordSpec.For)
            .ToList();
    }
}
