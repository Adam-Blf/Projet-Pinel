using System.Text;
using Pinel.Core.Export;
using Pinel.Core.Formats;

namespace Pinel.Tests;

/// <summary>
/// Chantier 1 du cahier des charges : un fichier ATIH à largeur fixe entre,
/// un CSV exploitable à séparateur point-virgule sort.
/// </summary>
public sealed class MoulinetteTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pinel-tests-" + Guid.NewGuid().ToString("N"));

    public MoulinetteTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private string WriteSource(params string[] lines)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var path = Path.Combine(_dir, "FV94_RPS_2025.txt");
        File.WriteAllLines(path, lines, Encoding.GetEncoding("ISO-8859-1"));
        return path;
    }

    private static FormatLayout Layout() => new("RPS", 2025, new[]
    {
        new FormatField("FINESS", 1, 9, "FINESS de l'établissement"),
        new FormatField("IPP", 10, 8, "Identifiant patient"),
        new FormatField("DATE_ACTE", 18, 8, "Date de l'acte"),
    });

    [Fact]
    public void Produit_un_csv_point_virgule_avec_une_colonne_par_champ()
    {
        var source = WriteSource(
            "940140049" + "12345678" + "20250114",
            "940140049" + "87654321" + "20250115");
        var output = Path.Combine(_dir, "sortie.csv");

        var result = RecordCsvExporter.Export(source, "RPS", output, Layout());

        Assert.Equal(2, result.Lines);
        Assert.False(result.RawFallback);

        var lines = File.ReadAllLines(output);
        Assert.Equal("FICHIER_SOURCE;NUM_LIGNE;FORMAT;FINESS;IPP;DATE_ACTE", lines[0]);
        Assert.Equal("FV94_RPS_2025.txt;1;RPS;940140049;12345678;20250114", lines[1]);
    }

    [Fact]
    public void Ecrit_un_bom_utf8_pour_qu_excel_ouvre_le_fichier_directement()
    {
        var source = WriteSource("940140049" + "12345678" + "20250114");
        var output = Path.Combine(_dir, "bom.csv");

        RecordCsvExporter.Export(source, "RPS", output, Layout());

        var head = File.ReadAllBytes(output).Take(3).ToArray();
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, head);
    }

    [Fact]
    public void Sans_descriptif_exporte_la_ligne_brute_plutot_que_d_inventer_un_decoupage()
    {
        var source = WriteSource("94014004912345678");
        var output = Path.Combine(_dir, "brut.csv");

        var result = RecordCsvExporter.Export(source, "RPS", output, layout: null);

        Assert.True(result.RawFallback);
        var lines = File.ReadAllLines(output);
        Assert.Equal("FICHIER_SOURCE;NUM_LIGNE;FORMAT;LIGNE_BRUTE", lines[0]);
        Assert.Contains("94014004912345678", lines[1]);
    }

    [Fact]
    public void Neutralise_les_valeurs_interpretees_comme_formule_par_excel()
    {
        Assert.Equal("'=SOMME(A1)", RecordCsvExporter.Escape("=SOMME(A1)"));
        Assert.Equal("\"a;b\"", RecordCsvExporter.Escape("a;b"));
    }

    [Fact]
    public void Le_descriptif_de_l_annee_exacte_prime_sur_les_autres()
    {
        var registry = new LayoutRegistry(Array.Empty<FormatLayout>());
        registry.Add(new FormatLayout("RPS", 2024, new[] { new FormatField("A", 1, 1) }));
        registry.Add(new FormatLayout("RPS", 2025, new[] { new FormatField("B", 1, 1) }));

        Assert.Equal("B", registry.Resolve("RPS", 2025)!.Fields[0].Name);
        Assert.Equal("A", registry.Resolve("RPS", 2024)!.Fields[0].Name);
        // Millésime inconnu : on retombe sur le descriptif antérieur le plus récent.
        Assert.Equal("B", registry.Resolve("RPS", 2026)!.Fields[0].Name);
    }

    [Fact]
    public void Aucun_descriptif_posterieur_n_est_applique_a_un_fichier_ancien()
    {
        // Le DIM n'a depose que 2024 et 2025. Un fichier de 2019 ne doit pas
        // etre decoupe avec les positions de 2025 : mieux vaut aucun descriptif,
        // donc un export brut signale, qu'un decoupage faux et silencieux.
        var registry = new LayoutRegistry(Array.Empty<FormatLayout>());
        registry.Add(new FormatLayout("RPS", 2024, new[] { new FormatField("A", 1, 1) }));
        registry.Add(new FormatLayout("RPS", 2025, new[] { new FormatField("B", 1, 1) }));

        Assert.Null(registry.Resolve("RPS", 2019));
    }

    [Fact]
    public void Un_descriptif_sans_annee_sert_de_recours_pour_un_fichier_ancien()
    {
        var registry = new LayoutRegistry(Array.Empty<FormatLayout>());
        registry.Add(new FormatLayout("RPS", 2025, new[] { new FormatField("B", 1, 1) }));
        registry.Add(new FormatLayout("RPS", null, new[] { new FormatField("GENERIQUE", 1, 1) }));

        Assert.Equal("GENERIQUE", registry.Resolve("RPS", 2019)!.Fields[0].Name);
    }

    [Fact]
    public void Un_descriptif_se_charge_depuis_un_fichier_depose_par_le_dim()
    {
        var path = Path.Combine(_dir, "RPS.format.csv");
        File.WriteAllText(path, string.Join('\n',
            "# format: RPS",
            "# annee: 2025",
            "nom;debut;longueur;libelle",
            "FINESS;1;9;FINESS",
            "IPP;10;8;Identifiant patient"), Encoding.UTF8);

        var layout = FormatLayout.Load(path);

        Assert.Equal("RPS", layout.Format);
        Assert.Equal(2025, layout.Year);
        Assert.Equal(2, layout.Fields.Count);
        Assert.Equal(17, layout.ExpectedLength);
    }
}
