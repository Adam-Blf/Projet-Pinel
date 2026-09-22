using Pinel.Core.Formats;
using Pinel.Core.Learning;

namespace Pinel.Tests;

/// <summary>
/// Apprentissage des corrections du DIM. Lignes synthetiques au format RAA
/// reduit : IPP en 22-41, date de l'acte en 70-77, UM en 60-63, forme
/// d'activite en 56-59, nature de l'acte en 78-79.
/// </summary>
public sealed class LearningTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 9, 22);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pinel-learn-" + Guid.NewGuid().ToString("N"));

    public LearningTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private static RecordSpec Spec() => RecordSpec.For(new FormatLayout("RAA", 2026, new[]
    {
        new FormatField("IPP", 22, 20, "N° d’identification permanent du patient (IPP)"),
        new FormatField("FORME", 56, 4, "Forme d'activité"),
        new FormatField("UM", 60, 4, "N° d’unité médicale"),
        new FormatField("DATE_ACTE", 70, 8, "Date de l’acte"),
        new FormatField("NATURE", 78, 2, "Nature de l’acte"),
        new FormatField("NDA", 95, 2, "Nombre de diagnostics et facteurs associés (nDA)"),
    }));

    private static string Line(int n, string forme, string um, string nature = "E")
    {
        var chars = new char[96];
        Array.Fill(chars, ' ');
        void Put(int start, string value) => value.CopyTo(0, chars, start - 1, value.Length);
        Put(22, $"IPP{n:0000000}");
        Put(56, forme);
        Put(60, um);
        Put(70, $"{1 + n % 28:00}012026");
        Put(78, nature);
        Put(95, "00");
        return new string(chars);
    }

    [Fact]
    public void Apprend_une_regle_sans_condition()
    {
        var before = Enumerable.Range(0, 20).Select(i => Line(i, "31", "U100")).ToList();
        var after = Enumerable.Range(0, 20).Select(i => Line(i, "33", "U100")).ToList();

        var rule = Assert.Single(CorrectionMiner.Mine(Spec(), before, after, Today).Rules);
        Assert.Equal(("FORME", "31", "33"), (rule.Field, rule.From, rule.To));
        Assert.Null(rule.ConditionField);
        Assert.Equal(20, rule.Support);
        Assert.Equal(1.0, rule.Confidence);
    }

    [Fact]
    public void Trouve_la_condition_qui_rend_la_correction_systematique()
    {
        // 31 devient 33 dans l'UM U100 seulement ; U200 garde 31.
        var before = Enumerable.Range(0, 20).Select(i => Line(i, "31", "U100"))
            .Concat(Enumerable.Range(20, 20).Select(i => Line(i, "31", "U200"))).ToList();
        var after = Enumerable.Range(0, 20).Select(i => Line(i, "33", "U100"))
            .Concat(Enumerable.Range(20, 20).Select(i => Line(i, "31", "U200"))).ToList();

        var rule = Assert.Single(CorrectionMiner.Mine(Spec(), before, after, Today).Rules);
        Assert.Equal(("UM", "U100"), (rule.ConditionField, rule.ConditionValue));
        Assert.Equal(20, rule.Support);
        Assert.Equal(0, rule.Contradictions);
    }

    [Fact]
    public void Aligne_par_cle_quand_des_lignes_sont_supprimees()
    {
        var before = Enumerable.Range(0, 30).Select(i => Line(i, "31", "U100")).ToList();
        var after = Enumerable.Range(0, 30).Where(i => i % 3 != 0).Select(i => Line(i, "33", "U100")).ToList();

        var mining = CorrectionMiner.Mine(Spec(), before, after, Today);
        Assert.Equal(10, mining.DeletedLines);
        Assert.Equal(20, mining.ChangedLines);
        Assert.Equal(20, Assert.Single(mining.Rules).Support);
    }

    [Fact]
    public void Une_regle_que_le_dim_n_applique_plus_perd_sa_confiance()
    {
        var store = new RuleStore();
        var before = Enumerable.Range(0, 20).Select(i => Line(i, "31", "U100")).ToList();
        store.Merge("mois1", CorrectionMiner.Mine(Spec(), before, before.Select(l => l.Replace("31  ", "33  ")).ToList(), Today).Rules);

        // Mois suivant : les memes lignes, laissees telles quelles.
        var later = Enumerable.Range(100, 20).Select(i => Line(i, "31", "U100")).ToList();
        store.Merge("mois2", CorrectionMiner.Mine(Spec(), later, later, Today, store.Rules).Rules);

        var rule = Assert.Single(store.Rules);
        Assert.Equal(20, rule.Support);
        Assert.Equal(20, rule.Contradictions);
        Assert.Equal(0.5, rule.Confidence);
    }

    [Fact]
    public void Une_meme_correction_recopiee_ne_compte_qu_une_fois()
    {
        var before = Enumerable.Range(0, 20).Select(i => Line(i, "31", "U100")).ToList();
        var after = before.Select(l => l.Replace("31  ", "33  ")).ToList();
        var evidence = new HashSet<string>();

        var first = CorrectionMiner.Mine(Spec(), before, after, Today, evidence: evidence);
        var copy = CorrectionMiner.Mine(Spec(), before, after, Today, evidence: evidence);

        Assert.Equal(20, Assert.Single(first.Rules).Support);
        Assert.Empty(copy.Rules);
    }

    [Fact]
    public void Le_statut_decide_par_le_dim_survit_a_un_nouvel_apprentissage()
    {
        var store = new RuleStore();
        var before = Enumerable.Range(0, 20).Select(i => Line(i, "31", "U100")).ToList();
        var after = before.Select(l => l.Replace("31  ", "33  ")).ToList();
        store.Merge("a", CorrectionMiner.Mine(Spec(), before, after, Today).Rules);
        store.Rules[0].Status = RuleStatus.Rejetee;

        Assert.False(store.Merge("a", CorrectionMiner.Mine(Spec(), before, after, Today).Rules)); // deja apprise
        var more = Enumerable.Range(50, 20).Select(i => Line(i, "31", "U100")).ToList();
        store.Merge("b", CorrectionMiner.Mine(Spec(), more, more.Select(l => l.Replace("31  ", "33  ")).ToList(), Today).Rules);

        Assert.Equal(RuleStatus.Rejetee, Assert.Single(store.Rules).Status);
        Assert.Equal(40, store.Rules[0].Support);
    }

    [Fact]
    public void Seule_une_regle_validee_corrige_la_copie()
    {
        var spec = Spec();
        var source = Path.Combine(_dir, "lot");
        Directory.CreateDirectory(source);
        File.WriteAllLines(Path.Combine(source, "raa.txt"),
            Enumerable.Range(0, 10).Select(i => Line(i, "31", "U100", "E")), PmsiEncoding.Latin1);

        LearnedRule Rule(string field, string from, string to, RuleStatus status) => new()
        {
            Format = "RAA", Field = field, From = from, To = to, Support = 50, Status = status,
        };
        var rules = new[]
        {
            Rule("FORME", "31", "33", RuleStatus.Validee),
            Rule("NATURE", "E", "D", RuleStatus.Proposee),
        };

        var output = Path.Combine(_dir, "copies");
        var hits = new RuleApplier(rules, new[] { spec }).Run(source, output);

        Assert.Equal(2, hits.Count);
        Assert.All(hits, h => Assert.Equal(10, h.Lines));
        var written = File.ReadAllLines(Path.Combine(output, "raa.txt"), PmsiEncoding.Latin1);
        Assert.All(written, l => Assert.Equal("33  ", l.Substring(55, 4)));
        Assert.All(written, l => Assert.Equal("E ", l.Substring(77, 2)));   // proposee : non appliquee
        Assert.All(written, l => Assert.Equal(96, l.Length));
    }

    [Theory]
    [InlineData("vh_psy_CORR_main.txt", "vh_psy")]
    [InlineData("vipp_CORRIGE_main.txt", "vipp")]
    [InlineData("raa_corrige_main.txt", "raa")]
    [InlineData("raa_202603270957.txt", "raa")]
    [InlineData("iso_202604211008 - Corriger.txt", "iso")]
    [InlineData("raa_corr_main___202608010800.txt", "raa")]
    public void Retrouve_le_nom_commun_d_un_fichier_et_de_sa_correction(string file, string stem)
    {
        Assert.Equal(stem, CorrectionPairFinder.Stem(file), ignoreCase: true);
    }
}
