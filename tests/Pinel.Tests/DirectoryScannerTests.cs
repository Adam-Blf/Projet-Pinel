using Pinel.Core.Formats;
using Pinel.Core.Processing;
using Xunit;

namespace Pinel.Tests;

/// <summary>
/// Deux corrections tirées du lot OSPI, douze ans de fichiers réels du GHT.
/// Le scanner ne retenait que les .txt et les .csv, alors que des fichiers PMSI
/// du lot n'ont aucune extension. Et il identifiait le format par le nom, ce qui
/// rate les conventions de nommage d'un établissement à l'autre.
/// </summary>
public sealed class DirectoryScannerTests : IDisposable
{
    private readonly string _dir;

    public DirectoryScannerTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "pinel-scan-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private string Ecrire(string nom, int longueur, int lignes = 40)
    {
        var chemin = Path.Combine(_dir, nom);
        Directory.CreateDirectory(Path.GetDirectoryName(chemin)!);
        File.WriteAllLines(chemin, Enumerable.Repeat(new string('0', longueur), lignes));
        return chemin;
    }

    /// <summary>
    /// Détecteur bâti sur des descriptifs complets. Les descriptifs intégrés ne
    /// déclarent que l'identifiant et la date de naissance : leur longueur
    /// attendue est celle du dernier champ décrit, pas celle de la ligne. Un
    /// détecteur construit sur eux ne reconnaîtrait donc rien, ce que ces tests
    /// ont montré avant d'être corrigés.
    /// </summary>
    private static ContentFormatDetector Detecteur()
    {
        var specs = new List<RecordSpec>
        {
            RecordSpec.For(Pleine("RPS", 154)),
            RecordSpec.For(Pleine("RAA", 96)),
            RecordSpec.For(Pleine("VID-IPP", 135)),
        };
        return new ContentFormatDetector(specs);
    }

    /// <summary>Descriptif d'un seul champ couvrant toute la ligne.</summary>
    private static FormatLayout Pleine(string format, int longueur) =>
        new(format, year: null, new[] { new FormatField("LIGNE", 1, longueur) });

    [Fact]
    public void Un_fichier_sans_extension_n_est_plus_ignore()
    {
        // Cas réel du lot : des fichiers nommés "vh" ou "VIDHOSP_PSY", sans
        // extension, disparaissaient du scan sans le moindre message.
        Ecrire(Path.Combine("2024", "vh"), 520);

        var vus = DirectoryScanner.Scan(new[] { _dir });

        Assert.Single(vus);
        Assert.Equal("vh", vus[0].Name);
    }

    [Fact]
    public void Un_classeur_ou_une_archive_reste_ecarte()
    {
        Ecrire("suivi.xlsx", 50);
        Ecrire("envoi.zip", 50);
        Ecrire("sejours.json", 50);
        Ecrire("rapport.pdf", 50);

        Assert.Empty(DirectoryScanner.Scan(new[] { _dir }));
    }

    [Fact]
    public void Sans_detecteur_le_format_vient_du_nom()
    {
        Ecrire("FV94_RPS_2026.txt", 154);

        var vus = DirectoryScanner.Scan(new[] { _dir });

        Assert.Equal("RPS", vus[0].Format);
    }

    [Fact]
    public void Le_contenu_reconnait_un_nom_que_le_nom_ne_sait_pas_lire()
    {
        // "er_psy_vip_2024_M12" : le motif du VID-IPP attend "vipp" ou "vid-ipp".
        // Par le nom, ce fichier restait INCONNU, et son absence faisait
        // ressortir tous les patients du RAA comme non chaînés.
        var chemin = Ecrire("er_psy_vip_2024_M12.txt", 135);

        Assert.Equal("INCONNU", DirectoryScanner.Scan(new[] { _dir })[0].Format);
        Assert.Equal("VID-IPP", DirectoryScanner.Scan(new[] { _dir }, Detecteur())[0].Format);
        Assert.True(File.Exists(chemin));
    }

    [Fact]
    public void Le_contenu_corrige_un_nom_qui_trompe()
    {
        // Le nom contient "RPS", la ligne fait la longueur d'un RAA. C'est la
        // ligne qui porte la vérité.
        Ecrire("extraction_RPS_du_mois.txt", 96);

        var vus = DirectoryScanner.Scan(new[] { _dir }, Detecteur());

        Assert.Equal("RAA", vus[0].Format);
    }

    [Fact]
    public void Un_fichier_que_le_contenu_ne_reconnait_pas_retombe_sur_le_nom()
    {
        // Millésime ancien : 152 caractères, aucune longueur déclarée ne
        // correspond. On ne perd pas le fichier pour autant, et la garde de
        // longueur dira ensuite que la structure ne colle pas.
        Ecrire("FV94_RPS_2019.txt", 152);

        var vus = DirectoryScanner.Scan(new[] { _dir }, Detecteur());

        Assert.Equal("RPS", vus[0].Format);
    }

    [Fact]
    public void Un_fichier_sans_extension_est_reconnu_par_son_contenu()
    {
        // Les deux corrections se rejoignent : sans extension ET sans sigle
        // dans le nom, seul le contenu peut trancher.
        Ecrire(Path.Combine("PSY", "vip"), 135);

        var vus = DirectoryScanner.Scan(new[] { _dir }, Detecteur());

        Assert.Equal("VID-IPP", vus[0].Format);
    }
}
