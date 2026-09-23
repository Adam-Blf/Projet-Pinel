using Pinel.Core.Formats;
using Pinel.Ml;

namespace Pinel.Tests;

/// <summary>
/// Modele des suppressions RAA : variables tirees du descriptif, etiquetage
/// par la correction du DIM, et promotion d'un modele seulement s'il fait au
/// moins aussi bien que celui en place. Lignes synthetiques.
/// </summary>
public sealed class DeletionModelTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pinel-ml-" + Guid.NewGuid().ToString("N"));

    public DeletionModelTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    /// <summary>Descriptif RAA reduit aux champs que le modele utilise, positions officielles 2026.</summary>
    private static RecordSpec Raa() => RecordSpec.For(new FormatLayout("RAA", 2026, new[]
    {
        new FormatField("FINESS_GEO", 10, 9, "N° FINESS géographique"),
        new FormatField("IPP", 22, 20, "N° d’identification permanent du patient (IPP)"),
        new FormatField("DDN", 42, 8, "Date de naissance du patient"),
        new FormatField("SEXE", 50, 1, "Sexe du patient"),
        new FormatField("CP", 51, 5, "Code postal de résidence"),
        new FormatField("FORME", 56, 4, "Forme d'activité"),
        new FormatField("UM", 60, 4, "N° d’unité médicale"),
        new FormatField("SECTEUR", 64, 5, "N° de secteur ou de dispositif"),
        new FormatField("LEGAL", 69, 1, "Mode légal de soins"),
        new FormatField("DATE_ACTE", 70, 8, "Date de l’acte"),
        new FormatField("NATURE", 78, 2, "Nature de l’acte"),
        new FormatField("LIEU", 80, 3, "Lieu de l’acte"),
        new FormatField("MODALITE", 83, 1, "Modalité de réalisation de l'acte"),
        new FormatField("CATEGORIE", 84, 1, "Catégorie professionnelle de l’intervenant"),
        new FormatField("INTERVENANTS", 85, 1, "Nombre d’intervenants"),
        new FormatField("LIBERAL", 86, 1, "Indicateur d’activité libérale"),
        new FormatField("DP", 87, 8, "Diagnostic principal ou motif de recours"),
        new FormatField("NDA", 95, 2, "Nombre de diagnostics et facteurs associés (nDA)"),
    }));

    private static string Line(int patient, string day, string nature = "E", string um = "5420", string dp = "F200")
    {
        var chars = new char[96];
        Array.Fill(chars, ' ');
        void Put(int start, string value) => value.CopyTo(0, chars, start - 1, value.Length);
        Put(1, "940140049");
        Put(10, "940000631");
        Put(22, $"IPP{patient:0000000}");
        Put(42, "15031985");
        Put(50, "1");
        Put(51, "94550");
        Put(56, "31  ");
        Put(60, um);
        Put(64, "94G01");
        Put(69, "1");
        Put(70, $"{day}012026");
        Put(78, nature.PadRight(2));
        Put(80, "L01");
        Put(83, "P");
        Put(84, "I");
        Put(85, "1");
        Put(86, " ");
        Put(87, dp.PadRight(8));
        Put(95, "00");
        return new string(chars);
    }

    [Fact]
    public void Lit_les_variables_d_apres_les_libelles_du_descriptif()
    {
        var builder = new RaaFeatureBuilder(Raa());
        var lines = new[] { Line(1, "12"), Line(1, "12"), Line(2, "13", nature: "A", um: "5443") };

        var examples = builder.Build(lines);

        Assert.Equal(3, examples.Count);
        Assert.Equal("5420", examples[0].Um);
        Assert.Equal("E", examples[0].Nature);
        Assert.Equal("F20", examples[0].ChapitreDp);
        Assert.Equal(41, examples[0].Age);              // acte en 2026, naissance en 1985
        Assert.Equal(1, examples[0].RangIdentique);
        Assert.Equal(2, examples[1].RangIdentique);     // deuxieme ligne strictement identique
        Assert.Equal(2, examples[1].ActesDuJour);
        Assert.Equal("5443", examples[2].Um);
        Assert.Equal(1, examples[2].RangNatureDuJour);
    }

    [Fact]
    public void Etiquette_comme_supprimees_les_lignes_absentes_du_fichier_corrige()
    {
        var builder = new RaaFeatureBuilder(Raa());
        var original = new[] { Line(1, "12"), Line(1, "12"), Line(2, "13") };
        var corrected = new[] { Line(1, "12"), Line(2, "13") };   // le DIM a retire la repetition

        var examples = builder.Build(original);
        builder.Label(examples, original, corrected);

        Assert.False(examples[0].Supprimee);
        Assert.True(examples[1].Supprimee);
        Assert.False(examples[2].Supprimee);
    }

    [Fact]
    public void Un_champ_absent_du_descriptif_est_dit_franchement()
    {
        var incomplet = RecordSpec.For(new FormatLayout("RAA", 2026, new[]
        {
            new FormatField("IPP", 22, 20, "N° d’identification permanent du patient (IPP)"),
        }));

        var erreur = Assert.Throws<InvalidOperationException>(() => new RaaFeatureBuilder(incomplet));
        Assert.Contains("introuvable", erreur.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Apprend_a_reperer_les_repetitions_du_jour_et_mesure_sur_un_mois_jamais_vu()
    {
        var (months, _) = Months(seed: 1);

        var (model, card, schema) = DeletionModel.Train(months);

        Assert.Equal("M3", card.HoldoutMonth);
        Assert.Equal(new[] { "M1", "M2" }, card.TrainingMonths);
        Assert.True(card.Auprc > 0.9, $"AUPRC attendue haute sur un motif net, lue : {card.Auprc:F3}");

        // Le mois de controle compte moins de suppressions que de lignes classees :
        // ce qui se mesure est donc la part des suppressions remontees en tete,
        // pas la precision brute, qui est bornee par leur nombre.
        double remonteesEnTete = card.PrecisionAtTop[100] * 100 / card.HoldoutDeletions;
        Assert.True(remonteesEnTete >= 0.9,
            $"{remonteesEnTete:P0} des {card.HoldoutDeletions} suppressions dans les 100 lignes les plus suspectes");
        Assert.NotNull(model);
        Assert.NotNull(schema);
    }

    [Fact]
    public void Ne_remplace_pas_le_modele_en_place_par_un_moins_bon()
    {
        var (months, _) = Months(seed: 2);
        var directory = Path.Combine(_dir, "modele");

        var champion = DeletionModel.Train(months);
        var first = DeletionModel.SaveIfBetter(champion.Model, champion.Schema, champion.Card, months[^1], directory);
        Assert.True(first.Promoted);
        Assert.Null(first.ChampionAuprc);

        // Challenger volontairement mauvais : meme mois de controle, AUPRC au ras du sol.
        var weak = new ModelCard
        {
            TrainedAt = DateTime.Now,
            TrainingMonths = champion.Card.TrainingMonths,
            HoldoutMonth = champion.Card.HoldoutMonth,
            HoldoutLines = champion.Card.HoldoutLines,
            HoldoutDeletions = champion.Card.HoldoutDeletions,
            Auc = 0.5,
            Auprc = 0.01,
        };

        var second = DeletionModel.SaveIfBetter(champion.Model, champion.Schema, weak, months[^1], directory);

        Assert.False(second.Promoted);
        Assert.Equal(champion.Card.Auprc, second.ChampionAuprc);
        var (_, kept) = DeletionModel.Load(directory);
        Assert.Equal(champion.Card.Auprc, kept.Auprc);
    }

    [Fact]
    public void Refuse_d_entrainer_avec_un_seul_mois()
    {
        var (months, _) = Months(seed: 3);

        var erreur = Assert.Throws<InvalidOperationException>(() => DeletionModel.Train(months.Take(1).ToList()));
        Assert.Contains("deux mois", erreur.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Trois mois ou le DIM supprime les actes d'accompagnement repetes le meme
    /// jour pour un meme patient, et garde tout le reste.
    /// </summary>
    private static (List<MonthExamples> Months, RaaFeatureBuilder Builder) Months(int seed)
    {
        var builder = new RaaFeatureBuilder(Raa());
        var random = new Random(seed);
        var months = new List<MonthExamples>();

        foreach (var name in new[] { "M1", "M2", "M3" })
        {
            var original = new List<string>();
            var corrected = new List<string>();
            for (int patient = 0; patient < 300; patient++)
            {
                var day = (1 + random.Next(28)).ToString("00");
                var nature = random.Next(4) == 0 ? "A" : "E";
                var line = Line(patient, day, nature);
                original.Add(line);
                corrected.Add(line);
                if (nature == "A" && random.Next(3) == 0)
                {
                    original.Add(line);   // repetition du jour : le DIM la retire
                }
                else if (random.Next(5) == 0)
                {
                    var autre = Line(patient, day, "E", um: "5443");
                    original.Add(autre);
                    corrected.Add(autre); // repetition gardee : nature et UM differentes
                }
            }
            var examples = builder.Build(original);
            builder.Label(examples, original, corrected);
            months.Add(new MonthExamples(name, examples));
        }
        return (months, builder);
    }
}
