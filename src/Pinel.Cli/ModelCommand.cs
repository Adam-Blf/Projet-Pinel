using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Pinel.Core.Formats;
using Pinel.Core.Learning;
using Pinel.Ml;

namespace Pinel.Cli;

/// <summary>
/// Commandes <c>entrainer</c> et <c>scorer</c> : modele de detection des
/// lignes RAA que le DIM supprime.
/// </summary>
internal static class ModelCommand
{
    public static int Train(string root, string modelDirectory, string formatsDirectory)
    {
        var specs = Specs.Load(formatsDirectory);
        var raa = specs.FirstOrDefault(s => s.Format == "RAA") ?? throw new IOException("Descriptif RAA absent.");
        var builder = new RaaFeatureBuilder(raa);

        // Une paire par mois : la correction la plus recente, sans quoi un mois
        // recorrige trois fois pesait trois fois.
        var months = new CorrectionPairFinder(specs).Find(root)
            .Where(p => p.Spec.Format == "RAA")
            .GroupBy(p => CorrectionPairFinder.MonthOf(p.Corrected))
            .Select(g => g.OrderByDescending(p => File.GetLastWriteTimeUtc(p.Corrected)).First())
            .OrderBy(p => MonthNumber(p.Corrected))
            .Select(p =>
            {
                var original = File.ReadAllLines(p.Original, PmsiEncoding.Latin1);
                var corrected = File.ReadAllLines(p.Corrected, PmsiEncoding.Latin1);
                var examples = builder.Build(original);
                builder.Label(examples, original, corrected);
                return new MonthExamples(Path.GetFileName(CorrectionPairFinder.MonthOf(p.Corrected)), examples);
            })
            .ToList();

        foreach (var month in months)
        {
            Console.WriteLine($"   {month.Month,-6} {month.Examples.Count,8} lignes, {month.Examples.Count(e => e.Supprimee),6} supprimees par le DIM");
        }

        var (model, card, schema) = DeletionModel.Train(months);
        Console.WriteLine();
        Console.WriteLine($"Controle sur {card.HoldoutMonth}, jamais vu a l'apprentissage ({card.HoldoutDeletions} suppressions sur {card.HoldoutLines} lignes) :");
        Console.WriteLine($"   AUC {card.Auc:F3}   AUPRC {card.Auprc:F3}   (hasard : {card.HoldoutDeletions / (double)card.HoldoutLines:F3})");
        foreach (var (k, precision) in card.PrecisionAtTop)
        {
            Console.WriteLine($"   {precision:P0} de vraies suppressions parmi les {k} lignes les plus suspectes");
        }

        var (promoted, champion) = DeletionModel.SaveIfBetter(model, schema, card, months[^1], modelDirectory);
        Console.WriteLine(champion is null
            ? "Premier modele : enregistre."
            : promoted
                ? $"Nouveau modele promu (champion precedent : AUPRC {champion:F3} sur le meme mois)."
                : $"Modele NON promu : le champion fait mieux (AUPRC {champion:F3}). Le champion reste en place.");
        return 0;
    }

    public static int Score(string folder, string modelDirectory, string formatsDirectory, string? reportPath)
    {
        var specs = Specs.Load(formatsDirectory);
        var raa = specs.First(s => s.Format == "RAA");
        var builder = new RaaFeatureBuilder(raa);
        var detector = new ContentFormatDetector(specs);
        var (engine, card) = DeletionModel.Load(modelDirectory);

        Console.WriteLine($"Modele du {card.TrainedAt:dd/MM/yyyy}, controle sur {card.HoldoutMonth} (AUPRC {card.Auprc:F3}), seuil {card.Threshold:F2}.");
        var report = new StringBuilder("fichier;ligne;probabilite_suppression\n");
        foreach (var path in Directory.EnumerateFiles(folder, "*.txt", SearchOption.AllDirectories).OrderBy(p => p))
        {
            if (detector.Detect(path).Spec?.Format != "RAA" || CorrectionPairFinder.IsCorrected(path)) continue;
            var lines = File.ReadAllLines(path, PmsiEncoding.Latin1);
            var examples = builder.Build(lines);
            int flagged = 0;
            for (int i = 0; i < examples.Count; i++)
            {
                var p = engine.Predict(examples[i]).Probabilite;
                if (p < card.Threshold) continue;
                flagged++;
                report.Append(CultureInfo.InvariantCulture, $"{Path.GetRelativePath(folder, path)};{i + 1};{p:F3}\n");
            }
            Console.WriteLine($"   {flagged,6} ligne(s) a revoir sur {lines.Length,7}  {Path.GetRelativePath(folder, path)}");
        }
        if (reportPath is not null)
        {
            File.WriteAllText(reportPath, report.ToString(), new UTF8Encoding(true));
            Console.WriteLine($"Rapport (numeros de ligne et probabilites, sans valeur de champ) : {reportPath}");
        }
        return 0;
    }

    private static int MonthNumber(string path)
    {
        var match = Regex.Match(Path.GetFileName(CorrectionPairFinder.MonthOf(path)), @"\d+");
        return match.Success ? int.Parse(match.Value, CultureInfo.InvariantCulture) : 0;
    }
}
