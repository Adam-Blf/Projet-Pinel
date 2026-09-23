using ClosedXML.Excel;
using Pinel.Core.Fichcomp;

namespace Pinel.Tests;

/// <summary>
/// Feuille Fichcomp transports : mapping des colonnes A à S depuis "Rapport 1
/// modifié", composition de la colonne M (ligne FICHCOMP à largeur fixe), et
/// signalement des dépassements de largeur.
/// </summary>
/// <remarks>
/// Tous les classeurs sont construits en mémoire avec des données fictives :
/// aucun fichier réel n'est ouvert, ces classeurs contiennent des données de
/// patients.
/// </remarks>
public sealed class TransportFichcompTests
{
    /// <summary>
    /// Constantes d'un etablissement fictif. Elles vivaient dans le code de
    /// production jusqu'au 23/09/2026 ; elles n'ont plus rien a y faire, Pinel
    /// servant n'importe quel departement d'information medicale.
    /// </summary>
    private static readonly TransportFichcompOptions Etablissement = TransportFichcompOptions.From(
        finessEPmsi: "940140049",
        finessGeographique: "940000631",
        typeDePrestation: "17",
        codeForfait: "ST2",
        classeDistance: "06");

    /// <summary>
    /// Construit une feuille source qui imite "Rapport 1 modifié" : un
    /// bandeau d'en-tête sur les trois premières lignes, puis les données à
    /// partir de la ligne 4, colonnes B à L.
    /// </summary>
    private static IXLWorksheet BuildSourceSheet(
        IXLWorkbook workbook,
        string uf = "6B01",
        string libelleUf = "Service test",
        string colonneF = "ABC123",
        string dateNaissance = "10031980",
        DateTime? dateCommande = null,
        string nomFournisseur = "Fournisseur Test",
        string adresse = "1 rue de Test",
        string codePostal = "94000",
        string ville = "Ville Test",
        string kilometres = "15,5")
    {
        var sheet = workbook.Worksheets.Add("Rapport 1 modifie");
        sheet.Cell(1, 1).Value = "Rapport 1 modifie (entete factice)";

        sheet.Cell(4, 2).Value = dateCommande ?? new DateTime(2025, 3, 10);
        sheet.Cell(4, 3).Value = uf;
        sheet.Cell(4, 4).Value = libelleUf;
        sheet.Cell(4, 6).Value = colonneF;
        sheet.Cell(4, 7).Value = dateNaissance;
        sheet.Cell(4, 8).Value = nomFournisseur;
        sheet.Cell(4, 9).Value = adresse;
        sheet.Cell(4, 10).Value = codePostal;
        sheet.Cell(4, 11).Value = ville;
        sheet.Cell(4, 12).Value = kilometres;

        return sheet;
    }

    [Fact]
    public void Chaque_colonne_A_a_S_vient_de_la_source_ou_de_la_constante_attendue()
    {
        using var workbook = new XLWorkbook();
        var source = BuildSourceSheet(workbook);
        var target = workbook.Worksheets.Add("Fichcomp");

        var result = TransportFichcompBuilder.Build(source, target, Etablissement);

        Assert.Equal(1, result.RowsWritten);
        Assert.True(result.IsClean);

        Assert.Equal("6B01", target.Cell(2, 1).GetString());              // A UF
        Assert.Equal("Service test", target.Cell(2, 2).GetString());      // B Libelle - Uf
        Assert.Equal("ABC123", target.Cell(2, 3).GetString());            // C
        Assert.Equal("10031980", target.Cell(2, 4).GetString());          // D Date de naissance
        Assert.Equal(string.Empty, target.Cell(2, 5).GetString());        // E IPP, vide
        Assert.Equal("940140049", target.Cell(2, 6).GetString());         // F Finess e-PMSI
        Assert.Equal("17", target.Cell(2, 7).GetString());                // G Type de prestation
        Assert.Equal(string.Empty, target.Cell(2, 8).GetString());        // H NDA, vide
        Assert.Equal("940000631", target.Cell(2, 9).GetString());         // I Finess geographique
        Assert.Equal(new DateTime(2025, 3, 10), target.Cell(2, 10).GetDateTime()); // J Date transp. Aller
        Assert.Equal("ST2", target.Cell(2, 11).GetString());              // K Code forfait
        Assert.Equal("06", target.Cell(2, 12).GetString());               // L Classe de distance
        Assert.False(string.IsNullOrEmpty(target.Cell(2, 13).GetString())); // M ligne FICHCOMP composee
        Assert.Equal("15,5", target.Cell(2, 14).GetString());             // N Nombre de kilometres
        Assert.Equal(string.Empty, target.Cell(2, 15).GetString());       // O Commentaire, vide
        Assert.Equal("Fournisseur Test", target.Cell(2, 16).GetString()); // P Nom fournisseur
        Assert.Equal("1 rue de Test", target.Cell(2, 17).GetString());    // Q Adresse fournisseur ligne 1
        Assert.Equal("94000", target.Cell(2, 18).GetString());            // R Code postal fournisseur
        Assert.Equal("Ville Test", target.Cell(2, 19).GetString());       // S Ville fournisseur
    }

    [Fact]
    public void La_ligne_2_de_fichcomp_correspond_a_la_ligne_4_de_rapport_1_modifie_puis_un_pour_un()
    {
        using var workbook = new XLWorkbook();
        var source = workbook.Worksheets.Add("Rapport 1 modifie");
        var target = workbook.Worksheets.Add("Fichcomp");

        for (int i = 0; i < 3; i++)
        {
            var sourceRow = 4 + i;
            source.Cell(sourceRow, 2).Value = new DateTime(2025, 1, 1 + i);
            source.Cell(sourceRow, 3).Value = $"UF{i}";
        }

        var result = TransportFichcompBuilder.Build(source, target, Etablissement);

        Assert.Equal(3, result.RowsWritten);
        Assert.Equal("UF0", target.Cell(2, 1).GetString());
        Assert.Equal("UF1", target.Cell(3, 1).GetString());
        Assert.Equal("UF2", target.Cell(4, 1).GetString());
    }

    [Fact]
    public void La_ligne_M_concatene_les_champs_completes_a_largeur_fixe()
    {
        var expected =
            "UF1".PadRight(TransportFichcompLayout.UfWidth)
            + "Libelle Test".PadRight(TransportFichcompLayout.LibelleWidth)
            + "COLC".PadRight(TransportFichcompLayout.ColumnCWidth)
            + "10031980".PadRight(TransportFichcompLayout.BirthDateWidth)
            + "10032025"
            + "940140049"
            + "17"
            + new string(' ', TransportFichcompLayout.TrailingSpaces);

        var line = TransportFichcompLayout.ComposeLine(
            "UF1", "Libelle Test", "COLC", "10031980",
            new DateOnly(2025, 3, 10), Etablissement);

        Assert.Equal(expected, line);
        Assert.StartsWith("UF1 ", line);
        Assert.Contains("10032025940140049", line);
    }

    [Fact]
    public void Un_finess_ou_une_classe_de_distance_differents_se_repercutent_sur_M_et_sur_les_colonnes()
    {
        var options = new TransportFichcompOptions(
            FinessEPmsi: "123456789",
            TypeDePrestation: "42",
            FinessGeographique: "987654321",
            CodeForfait: "AUTRE",
            ClasseDistance: "12");

        using var workbook = new XLWorkbook();
        var source = BuildSourceSheet(workbook);
        var target = workbook.Worksheets.Add("Fichcomp");

        TransportFichcompBuilder.Build(source, target, options);

        Assert.Equal("123456789", target.Cell(2, 6).GetString());
        Assert.Equal("987654321", target.Cell(2, 9).GetString());
        Assert.Equal("AUTRE", target.Cell(2, 11).GetString());
        Assert.Equal("12", target.Cell(2, 12).GetString());
        Assert.EndsWith("123456789" + "42" + new string(' ', TransportFichcompLayout.TrailingSpaces),
            target.Cell(2, 13).GetString());
    }

    [Fact]
    public void Un_uf_trop_long_est_signale_et_jamais_tronque_en_silence()
    {
        using var workbook = new XLWorkbook();
        var source = BuildSourceSheet(workbook, uf: "UF_BEAUCOUP_TROP_LONGUE");
        var target = workbook.Worksheets.Add("Fichcomp");

        var result = TransportFichcompBuilder.Build(source, target, Etablissement);

        Assert.Equal(1, result.RowsWritten);
        Assert.False(result.IsClean);
        Assert.Contains(result.Issues, issue => issue.Message.Contains("UF", StringComparison.OrdinalIgnoreCase));

        // La colonne A n'a pas de largeur fixe : la valeur complete doit y
        // rester intacte, sans troncature silencieuse.
        Assert.Equal("UF_BEAUCOUP_TROP_LONGUE", target.Cell(2, 1).GetString());

        // La colonne M ne peut pas etre composee : elle reste vide plutot
        // que de porter une ligne tronquee et donc fausse.
        Assert.Equal(string.Empty, target.Cell(2, 13).GetString());
    }

    [Fact]
    public void Une_date_de_transport_absente_est_signalee_sans_bloquer_les_autres_colonnes()
    {
        using var workbook = new XLWorkbook();
        var source = BuildSourceSheet(workbook, dateCommande: null);
        source.Cell(4, 2).Clear(); // Retire la date de commande deposee par le constructeur.
        var target = workbook.Worksheets.Add("Fichcomp");

        var result = TransportFichcompBuilder.Build(source, target, Etablissement);

        Assert.Equal(1, result.RowsWritten);
        Assert.False(result.IsClean);
        Assert.Contains(result.Issues, issue => issue.Message.Contains("date", StringComparison.OrdinalIgnoreCase));

        Assert.Equal("6B01", target.Cell(2, 1).GetString());
        Assert.Equal(string.Empty, target.Cell(2, 13).GetString());
    }

    [Fact]
    public void Un_classeur_sans_lignes_de_donnees_ne_produit_que_len_tete()
    {
        using var workbook = new XLWorkbook();
        var source = workbook.Worksheets.Add("Rapport 1 modifie");
        var target = workbook.Worksheets.Add("Fichcomp");

        var result = TransportFichcompBuilder.Build(source, target, Etablissement);

        Assert.Equal(0, result.RowsWritten);
        Assert.True(result.IsClean);
        Assert.Equal("UF", target.Cell(1, 1).GetString());
        Assert.Equal(string.Empty, target.Cell(2, 1).GetString());
    }
}
