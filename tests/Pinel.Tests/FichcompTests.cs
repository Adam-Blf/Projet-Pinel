using Pinel.Core.Fichcomp;

namespace Pinel.Tests;

/// <summary>
/// Fichier complémentaire : mise en forme à largeur fixe, relecture et
/// contrôle. Règles reprises de la moulinette Excel utilisée au DIM.
/// </summary>
public sealed class FichcompTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pinel-fichcomp-" + Guid.NewGuid().ToString("N"));

    public FichcompTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void La_ligne_medicament_fait_53_caracteres()
    {
        var line = FichcompConverter.ToLine(
            new FichcompRecord("940140049", "SEJ-2025-001", "3400891", 1.5m, new DateOnly(2025, 3, 10)),
            FichcompLayout.Medicament);

        Assert.Equal(53, line.Length);
        Assert.StartsWith("940140049", line);
        Assert.EndsWith("10032025", line);
    }

    [Fact]
    public void La_quantite_est_portee_au_millieme_sur_sept_caracteres()
    {
        Assert.Equal("0001500", FichcompConverter.FormatQuantity(1.5m, FichcompLayout.Medicament));
        Assert.Equal("0000001", FichcompConverter.FormatQuantity(0.001m, FichcompLayout.Medicament));
    }

    [Fact]
    public void La_ligne_dispositif_fait_50_caracteres_et_une_quantite_entiere()
    {
        var line = FichcompConverter.ToLine(
            new FichcompRecord("940140049", "SEJ-2025-001", "3400891", 2m, null),
            FichcompLayout.DispositifMedical);

        Assert.Equal(50, line.Length);
        Assert.Equal("0002", line.Substring(FichcompLayout.DispositifMedical.QuantityStart, 4));
    }

    [Fact]
    public void Une_date_absente_donne_huit_espaces()
    {
        var line = FichcompConverter.ToLine(
            new FichcompRecord("940140049", "SEJ", "3400891", 1m, null),
            FichcompLayout.Medicament);

        Assert.Equal("        ", line[^8..]);
    }

    [Fact]
    public void Le_finess_est_complete_a_gauche_par_des_zeros()
    {
        var line = FichcompConverter.ToLine(
            new FichcompRecord("1234", "SEJ", "1", 1m, null),
            FichcompLayout.Medicament);

        Assert.StartsWith("000001234", line);
    }

    [Fact]
    public void Ecriture_puis_relecture_conservent_les_valeurs()
    {
        var path = Path.Combine(_dir, "fichcomp_med.txt");
        var record = new FichcompRecord("940140049", "SEJ-2025-001", "3400891", 2.25m, new DateOnly(2025, 6, 1));

        FichcompConverter.Write(new[] { record }, path, FichcompLayout.Medicament);
        var back = Assert.Single(FichcompConverter.Read(path, FichcompLayout.Medicament));

        Assert.Equal(record.Finess, back.Finess);
        Assert.Equal(record.StayNumber, back.StayNumber);
        Assert.Equal("003400891", back.Code);
        Assert.Equal(2.25m, back.Quantity);
        Assert.Equal(record.Date, back.Date);
    }

    [Fact]
    public void Le_controle_signale_une_ligne_trop_courte()
    {
        var path = Path.Combine(_dir, "court.txt");
        File.WriteAllText(path, "940140049TROP-COURT\n");

        var result = FichcompConverter.Check(path, FichcompLayout.Medicament);

        Assert.False(result.IsClean);
        Assert.Contains(result.Issues, i => i.Message.Contains("longueur"));
    }

    [Fact]
    public void Le_controle_accepte_un_fichier_conforme()
    {
        var path = Path.Combine(_dir, "conforme.txt");
        FichcompConverter.Write(
            new[] { new FichcompRecord("940140049", "SEJ", "3400891", 1m, new DateOnly(2025, 1, 2)) },
            path,
            FichcompLayout.Medicament);

        var result = FichcompConverter.Check(path, FichcompLayout.Medicament);

        Assert.True(result.IsClean);
        Assert.Equal(1, result.Lines);
    }

    [Fact]
    public void Une_quantite_hors_capacite_leve_au_lieu_de_saturer()
    {
        // 12000 unites ne tiennent pas sur 7 caracteres au millieme. Ecrire
        // 9999999 donnerait une quantite plausible et fausse dans le fichier
        // transmis a la facturation.
        Assert.Throws<FichcompOverflowException>(
            () => FichcompConverter.FormatQuantity(12000m, FichcompLayout.Medicament));
    }

    [Fact]
    public void Un_numero_de_sejour_trop_long_leve_au_lieu_d_etre_tronque()
    {
        var trop_long = new string('A', 25);
        var exception = Assert.Throws<FichcompOverflowException>(
            () => FichcompConverter.ToLine(
                new FichcompRecord("940140049", trop_long, "3400891", 1m, null),
                FichcompLayout.Medicament));

        Assert.Contains("sejour", exception.Message);
    }

    [Fact]
    public void Une_ligne_trop_courte_ne_produit_qu_une_anomalie_de_longueur()
    {
        var path = Path.Combine(_dir, "courte.txt");
        File.WriteAllLines(path, new[] { "94014004" });

        var result = FichcompConverter.Check(path, FichcompLayout.Medicament);

        var issue = Assert.Single(result.Issues);
        Assert.Contains("longueur", issue.Message);
    }

    [Fact]
    public void Une_ligne_d_en_tete_repetee_est_reconnue()
    {
        Assert.True(TransportWorkbookCleaner.IsHeaderRow(TransportWorkbookCleaner.HeaderLabels));
        Assert.False(TransportWorkbookCleaner.IsHeaderRow(new[] { "10/01/2025", "AMBULANCE", "12" }));
    }
}
