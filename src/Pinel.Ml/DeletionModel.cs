using System.Text.Json;
using Microsoft.ML;
using Microsoft.ML.Trainers.LightGbm;

namespace Pinel.Ml;

/// <summary>Mois d'apprentissage : les lignes d'origine d'un envoi, etiquetees par la correction du DIM.</summary>
public sealed record MonthExamples(string Month, List<RaaExample> Examples);

/// <summary>Fiche d'un modele : ce qu'il a appris, sur quoi, et ce qu'il vaut.</summary>
public sealed class ModelCard
{
    public DateTime TrainedAt { get; set; }
    public List<string> TrainingMonths { get; set; } = new();
    public string HoldoutMonth { get; set; } = "";
    public int TrainingLines { get; set; }
    public int HoldoutLines { get; set; }
    public int HoldoutDeletions { get; set; }
    public double Auc { get; set; }
    /// <summary>Aire sous la courbe precision / rappel : la mesure qui compte quand 0,5 % des lignes sont supprimees.</summary>
    public double Auprc { get; set; }
    /// <summary>Part de vraies suppressions parmi les N lignes les plus suspectes du mois de controle.</summary>
    public Dictionary<int, double> PrecisionAtTop { get; set; } = new();
    public double Threshold { get; set; }
}

/// <summary>
/// Modele de detection des lignes RAA que le DIM supprime.
/// </summary>
/// <remarks>
/// <para>
/// Arbres de decision boostes (LightGBM), le choix de reference pour des
/// donnees tabulaires faites de codes et de decomptes : plus precis qu'un
/// reseau de neurones a ce volume, explicables variable par variable, et
/// executes par ML.NET sur le poste DIM sans Python ni acces reseau.
/// </para>
/// <para>
/// Validation temporelle : le dernier mois disponible est mis de cote et le
/// modele est appris sur les precedents. C'est la seule mesure honnete de ce
/// que vaudra le modele sur le mois suivant ; un tirage aleatoire des lignes
/// melangerait le passe et l'avenir.
/// </para>
/// <para>
/// Amelioration dans le temps : chaque mois apporte une paire origine /
/// corrige de plus. Un modele nouvellement entraine ne remplace l'actuel
/// (champion) que s'il fait au moins aussi bien que lui sur le meme mois de
/// controle : un reentrainement ne peut pas degrader silencieusement les
/// suggestions.
/// </para>
/// </remarks>
public static class DeletionModel
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static (ITransformer Model, ModelCard Card, DataViewSchema Schema) Train(IReadOnlyList<MonthExamples> months, int seed = 2026)
    {
        if (months.Count < 2) throw new InvalidOperationException("Il faut au moins deux mois corriges : un pour apprendre, un pour controler.");

        var ml = new MLContext(seed);
        var holdout = months[^1];
        var training = months.Take(months.Count - 1).ToList();
        var train = ml.Data.LoadFromEnumerable(training.SelectMany(m => m.Examples));
        var test = ml.Data.LoadFromEnumerable(holdout.Examples);

        var model = Pipeline(ml).Fit(train);
        var scored = ml.Data.CreateEnumerable<ScoredExample>(model.Transform(test), reuseRowObject: false).ToList();
        var metrics = ml.BinaryClassification.Evaluate(model.Transform(test));

        var card = new ModelCard
        {
            TrainedAt = DateTime.Now,
            TrainingMonths = training.Select(m => m.Month).ToList(),
            HoldoutMonth = holdout.Month,
            TrainingLines = training.Sum(m => m.Examples.Count),
            HoldoutLines = holdout.Examples.Count,
            HoldoutDeletions = holdout.Examples.Count(e => e.Supprimee),
            Auc = metrics.AreaUnderRocCurve,
            Auprc = metrics.AreaUnderPrecisionRecallCurve,
        };
        var ranked = scored.OrderByDescending(s => s.Probability).ToList();
        foreach (var k in new[] { 100, 500, 1000 }.Where(k => k <= ranked.Count))
        {
            card.PrecisionAtTop[k] = ranked.Take(k).Count(s => s.Label) / (double)k;
        }
        card.Threshold = BestThreshold(ranked);

        // Modele final : appris sur tous les mois, controle compris. La fiche
        // garde la mesure faite avant d'y inclure le mois de controle.
        var all = ml.Data.LoadFromEnumerable(months.SelectMany(m => m.Examples));
        var final = Pipeline(ml).Fit(all);
        return (final, card, all.Schema);
    }

    private static IEstimator<ITransformer> Pipeline(MLContext ml)
    {
        var categorical = RaaExample.Categorical
            .Select(c => new InputOutputColumnPair(c + "Code", c))
            .ToArray();
        return ml.Transforms.Categorical.OneHotEncoding(categorical)
            .Append(ml.Transforms.Concatenate("Features",
                RaaExample.Categorical.Select(c => c + "Code").Concat(RaaExample.Numeric).ToArray()))
            .Append(ml.BinaryClassification.Trainers.LightGbm(new LightGbmBinaryTrainer.Options
            {
                NumberOfLeaves = 31,
                MinimumExampleCountPerLeaf = 30,
                LearningRate = 0.05,
                NumberOfIterations = 400,
                // Classes tres desequilibrees (moins de 1 % de suppressions).
                UnbalancedSets = true,
                Seed = 2026,
            }));
    }

    /// <summary>Seuil qui maximise le F1 sur le mois de controle.</summary>
    private static double BestThreshold(IReadOnlyList<ScoredExample> ranked)
    {
        int positives = ranked.Count(s => s.Label);
        if (positives == 0) return 0.5;
        double best = 0, threshold = 0.5;
        int truePositives = 0;
        for (int i = 0; i < ranked.Count; i++)
        {
            if (ranked[i].Label) truePositives++;
            double precision = truePositives / (double)(i + 1);
            double recall = truePositives / (double)positives;
            double f1 = precision + recall == 0 ? 0 : 2 * precision * recall / (precision + recall);
            if (f1 > best) { best = f1; threshold = ranked[i].Probability; }
        }
        return threshold;
    }

    /// <summary>
    /// Enregistre le modele s'il vaut au moins le champion en place. Le champion
    /// est remesure sur le meme mois de controle que le nouveau modele, mois
    /// qu'aucun des deux n'a vu a l'apprentissage du nouveau. Rend vrai si le
    /// modele a ete promu, et l'AUPRC du champion sur ce mois (null sans champion).
    /// </summary>
    public static (bool Promoted, double? ChampionAuprc) SaveIfBetter(ITransformer model, DataViewSchema schema, ModelCard card,
        MonthExamples holdout, string directory, double tolerance = 0.01)
    {
        Directory.CreateDirectory(directory);
        var modelPath = Path.Combine(directory, "modele.zip");
        double? championAuprc = null;
        var cardPath = Path.Combine(directory, "modele.json");
        if (File.Exists(modelPath) && File.Exists(cardPath))
        {
            var championCard = JsonSerializer.Deserialize<ModelCard>(File.ReadAllText(cardPath));
            if (championCard?.HoldoutMonth == holdout.Month)
            {
                // Meme mois de controle : la mesure du champion, faite avant qu'il
                // n'apprenne ce mois, est la bonne ; le remesurer maintenant
                // serait le noter sur un mois qu'il connait.
                championAuprc = championCard.Auprc;
            }
            else
            {
                var ml = new MLContext();
                var champion = ml.Model.Load(modelPath, out _);
                var data = ml.Data.LoadFromEnumerable(holdout.Examples);
                championAuprc = ml.BinaryClassification.Evaluate(champion.Transform(data)).AreaUnderPrecisionRecallCurve;
            }
            if (card.Auprc < championAuprc.Value - tolerance) return (false, championAuprc);
        }

        var history = Path.Combine(directory, "historique");
        Directory.CreateDirectory(history);
        File.WriteAllText(Path.Combine(history, $"modele-{card.TrainedAt:yyyyMMdd-HHmmss}.json"), JsonSerializer.Serialize(card, Json));

        new MLContext().Model.Save(model, schema, modelPath);
        File.WriteAllText(cardPath, JsonSerializer.Serialize(card, Json));
        return (true, championAuprc);
    }

    public static (PredictionEngine<RaaExample, RaaPrediction> Engine, ModelCard Card) Load(string directory)
    {
        var ml = new MLContext();
        var model = ml.Model.Load(Path.Combine(directory, "modele.zip"), out _);
        var card = JsonSerializer.Deserialize<ModelCard>(File.ReadAllText(Path.Combine(directory, "modele.json")))
                   ?? throw new InvalidOperationException("Fiche du modele illisible.");
        return (ml.Model.CreatePredictionEngine<RaaExample, RaaPrediction>(model), card);
    }

    private sealed class ScoredExample
    {
        public bool Label { get; set; }
        public float Probability { get; set; }
    }
}
