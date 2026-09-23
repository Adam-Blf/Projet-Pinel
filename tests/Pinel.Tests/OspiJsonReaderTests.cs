using Pinel.Core.Formats;
using Xunit;

namespace Pinel.Tests;

/// <summary>
/// Lecture des exports JSON de la plateforme OSPI. Relevé sur les exports réels
/// du GHT : un export n'est pas un format ATIH de plus, c'est un groupement déjà
/// fait, où chaque enregistrement rassemble un patient ou un séjour avec toutes
/// ses lignes ATIH et sa clé de chaînage.
/// </summary>
public sealed class OspiJsonReaderTests : IDisposable
{
    private readonly string _dir;

    public OspiJsonReaderTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "pinel-ospi-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    /// <summary>Valeurs entièrement factices, de la forme des vraies.</summary>
    private string EcrireExport(string nom, string json)
    {
        var chemin = Path.Combine(_dir, nom);
        File.WriteAllText(chemin, json);
        return chemin;
    }

    private static string Ligne(int longueur, char c = 'A') => new(c, longueur);

    private string ExportAmbulatoire(int patients = 2)
    {
        var records = Enumerable.Range(0, patients).Select(i => $$"""
        {
          "ipp": "{{Ligne(20, (char)('A' + i))}}",
          "dateNaissance": "01/01/1900",
          "anoipp": "{{Ligne(1584, 'N')}}",
          "raas": ["{{Ligne(96, 'R')}}", "{{Ligne(104, 'R')}}"],
          "iums": ["{{Ligne(38, 'U')}}"],
          "sexe": "1",
          "codePostal": "00000"
        }
        """);
        return "[" + string.Join(",", records) + "]";
    }

    [Fact]
    public void Un_export_ospi_est_reconnu_sans_etre_charge()
    {
        var chemin = EcrireExport("ambu.json", ExportAmbulatoire());

        Assert.True(OspiJsonReader.LooksLikeOspi(chemin));
    }

    [Fact]
    public void Un_json_quelconque_n_est_pas_pris_pour_un_export()
    {
        Assert.False(OspiJsonReader.LooksLikeOspi(EcrireExport("reglages.json", """{"dossier":"D:\\PMSI"}""")));
        Assert.False(OspiJsonReader.LooksLikeOspi(EcrireExport("liste.json", """[1,2,3]""")));
        Assert.False(OspiJsonReader.LooksLikeOspi(EcrireExport("vide.json", "[]")));
        Assert.False(OspiJsonReader.LooksLikeOspi(EcrireExport("casse.json", "{ ceci n'est pas du json")));
    }

    [Fact]
    public void Les_lignes_sortent_dans_un_fichier_par_format()
    {
        var chemin = EcrireExport("940000000.2025.06.PSY.AMBU.json", ExportAmbulatoire(patients: 3));
        var sortie = Path.Combine(_dir, "sortie");

        var extraction = OspiJsonReader.Extract(chemin, sortie);

        Assert.Equal(3, extraction.Records);
        // Deux RAA par patient.
        Assert.Equal(6, extraction.LinesByFormat["RAA"]);
        // Une cle de chainage par patient.
        Assert.Equal(3, extraction.LinesByFormat["ANO-HOSP"]);

        var fichiers = Directory.GetFiles(sortie).Select(Path.GetFileName).ToList();
        Assert.Contains("940000000.2025.06.PSY.AMBU.RAA.txt", fichiers);
        Assert.Contains("940000000.2025.06.PSY.AMBU.ANO-HOSP.txt", fichiers);
    }

    [Fact]
    public void Les_longueurs_des_lignes_sont_conservees_telles_quelles()
    {
        // Les zones repetees font varier la longueur : un RAA fait 96 ou 104
        // selon le nombre de diagnostics associes. Rien ne doit etre normalise.
        var chemin = EcrireExport("ambu.json", ExportAmbulatoire(patients: 1));
        var sortie = Path.Combine(_dir, "sortie");

        OspiJsonReader.Extract(chemin, sortie);

        var longueurs = File.ReadAllLines(Path.Combine(sortie, "ambu.RAA.txt"))
            .Select(l => l.Length).OrderBy(x => x).ToList();
        Assert.Equal(new[] { 96, 104 }, longueurs);
    }

    [Fact]
    public void Une_ligne_d_unite_medicale_n_est_ecrite_qu_une_fois()
    {
        // L'export attache la description de l'unite a chacun de ses patients.
        // Sur un mois reel, cela faisait 21 563 lignes pour 108 unites, et les
        // controles de doublons signalaient un biais d'activite.
        var chemin = EcrireExport("ambu.json", ExportAmbulatoire(patients: 5));
        var sortie = Path.Combine(_dir, "sortie");

        var extraction = OspiJsonReader.Extract(chemin, sortie);

        Assert.Equal(1, extraction.LinesByFormat["FICUM-PSY"]);
        Assert.Single(File.ReadAllLines(Path.Combine(sortie, "ambu.FICUM-PSY.txt")));
    }

    [Fact]
    public void Deux_lignes_d_activite_identiques_sont_toutes_les_deux_conservees()
    {
        // Deux entretiens identiques le meme jour sont deux actes reels : une
        // ligne de RAA ne porte ni heure ni identifiant d'acte.
        var record = $$"""
        { "ipp": "{{Ligne(20)}}", "raas": ["{{Ligne(96, 'R')}}", "{{Ligne(96, 'R')}}"] }
        """;
        var chemin = EcrireExport("ambu.json", "[" + record + "]");
        var sortie = Path.Combine(_dir, "sortie");

        var extraction = OspiJsonReader.Extract(chemin, sortie);

        Assert.Equal(2, extraction.LinesByFormat["RAA"]);
    }

    [Fact]
    public void Un_champ_nul_ou_absent_ne_produit_aucun_fichier()
    {
        // Les exports reels portent "isolconts": null quand il n'y a rien.
        var record = $$"""
        { "ipp": "{{Ligne(20)}}", "raas": ["{{Ligne(96, 'R')}}"], "isolconts": null, "transps": null }
        """;
        var chemin = EcrireExport("sejours.json", "[" + record + "]");
        var sortie = Path.Combine(_dir, "sortie");

        var extraction = OspiJsonReader.Extract(chemin, sortie);

        Assert.False(extraction.LinesByFormat.ContainsKey("FICHCOMP-ISO"));
        Assert.False(extraction.LinesByFormat.ContainsKey("FICHCOMP"));
        Assert.Empty(Directory.GetFiles(sortie, "*FICHCOMP*"));
    }
}
