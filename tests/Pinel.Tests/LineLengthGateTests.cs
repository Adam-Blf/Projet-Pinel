using Pinel.Core.Checks;
using Xunit;

namespace Pinel.Tests;

/// <summary>
/// Garde de longueur de ligne. Mesurée sur le lot OSPI, douze ans de fichiers
/// réels du GHT : un dossier de 2024 produisait 734 anomalies, dont 7 549
/// chaînages manquants comptés sur cinq cents plus un résumé, uniquement parce
/// que le VID-HOSP de 2024 fait 468 caractères là où la matrice en déclare 520.
/// Après la garde, 23 anomalies, dont une seule qui dit la vérité.
/// </summary>
public sealed class LineLengthGateTests : IDisposable
{
    private readonly string _dir;

    public LineLengthGateTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "pinel-longueur-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private string Ecrire(string nom, int longueur, int lignes, char remplissage = '0')
    {
        var chemin = Path.Combine(_dir, nom);
        File.WriteAllLines(chemin, Enumerable.Range(0, lignes).Select(_ => new string(remplissage, longueur)));
        return chemin;
    }

    [Fact]
    public void Une_longueur_conforme_passe_sans_rien_signaler()
    {
        // RPS canonique : 154 caractères.
        var verdict = LineLengthGate.Inspect(Ecrire("FV_RPS_2026.txt", 154, 50), "RPS");

        Assert.True(verdict.Accepted);
        Assert.Null(verdict.Finding);
        Assert.Equal(154, verdict.ObservedLength);
    }

    [Fact]
    public void Une_variante_declaree_passe_aussi()
    {
        // Le RPS déclare deux longueurs de transition en plus de la canonique.
        var verdict = LineLengthGate.Inspect(Ecrire("FV_RPS_2021.txt", 148, 50), "RPS");

        Assert.True(verdict.Accepted);
        Assert.Null(verdict.Finding);
    }

    [Fact]
    public void Le_vid_hosp_de_2024_est_refuse_avec_les_deux_longueurs_dans_le_message()
    {
        // Le cas réel : 468 caractères relevés, 520 déclarés.
        var verdict = LineLengthGate.Inspect(Ecrire("er_psy_vh_2024_M12.txt", 468, 200), "VID-HOSP");

        Assert.False(verdict.Accepted);
        Assert.NotNull(verdict.Finding);
        Assert.Equal(LineLengthGate.Code, verdict.Finding!.Code);
        Assert.Equal(CheckSeverity.Error, verdict.Finding.Severity);
        // Le message doit nommer ce qui est lu ET ce qui est attendu : une garde
        // qui dit seulement « longueur inattendue » n'est pas actionnable.
        Assert.Contains("468", verdict.Finding.Message);
        Assert.Contains("520", verdict.Finding.Message);
    }

    [Fact]
    public void Un_format_inconnu_de_la_matrice_ne_declenche_rien()
    {
        var verdict = LineLengthGate.Inspect(Ecrire("mystere.txt", 42, 10), "FORMAT-QUI-N-EXISTE-PAS");

        Assert.True(verdict.Accepted);
        Assert.Null(verdict.Finding);
    }

    [Fact]
    public void Un_fichier_vide_ne_declenche_rien()
    {
        var chemin = Path.Combine(_dir, "vide.txt");
        File.WriteAllText(chemin, string.Empty);

        Assert.True(LineLengthGate.Inspect(chemin, "RPS").Accepted);
    }

    [Fact]
    public void Un_fichier_heterogene_mais_majoritairement_conforme_passe()
    {
        // Un format a zones repetees fait varier la longueur en toute
        // legitimite. Tant que la majorite des lignes est conforme, on accepte.
        var chemin = Path.Combine(_dir, "melange.txt");
        File.WriteAllLines(chemin, new[]
        {
            new string('0', 154), new string('0', 154), new string('0', 154),
            new string('0', 162), new string('0', 170),
        });

        Assert.True(LineLengthGate.Inspect(chemin, "RPS").Accepted);
    }

    [Fact]
    public void Un_fichier_heterogene_sans_ligne_conforme_est_refuse()
    {
        // Le trou ferme le 23/09/2026 : un ANO-HOSP de 2020 n'avait aucune
        // longueur dominante, et aucune de ses lignes n'etait a la longueur
        // declaree. Il passait pourtant, et les controles de NIR lisaient
        // ensuite la position 1 d'un fichier qui n'y porte pas de NIR.
        var chemin = Path.Combine(_dir, "fv_psy_ano_2020_M12.txt");
        File.WriteAllLines(chemin, new[]
        {
            new string('0', 1283), new string('0', 1283), new string('0', 1283),
            new string('0', 900), new string('0', 1100), new string('0', 1400),
        });

        var verdict = LineLengthGate.Inspect(chemin, "ANO-HOSP");

        Assert.False(verdict.Accepted);
        Assert.NotNull(verdict.Finding);
        Assert.Contains("1064", verdict.Finding!.Message);
    }

    [Fact]
    public void Le_lanceur_suspend_les_controles_de_position_et_garde_les_autres()
    {
        // Un VID-HOSP au millésime 2024 : les contrôles de NIR lisent des
        // positions, le contrôle de doublons compare des lignes entières.
        var chemin = Path.Combine(_dir, "er_psy_vh_2024_M12.txt");
        var ligne = new string('0', 468);
        File.WriteAllLines(chemin, Enumerable.Repeat(ligne, 30));

        var constats = new CheckRunner().Run(new[] { (chemin, "VID-HOSP") });

        Assert.Contains(constats, f => f.Code == LineLengthGate.Code);
        // Les contrôles de position se taisent : aucune anomalie de NIR ni de FINESS.
        Assert.DoesNotContain(constats, f => f.Code.StartsWith("ERR-VID-NIR", StringComparison.Ordinal));
        Assert.DoesNotContain(constats, f => f.Code == "ERR-FINESS-FORMAT");
        // Le contrôle de doublons, lui, reste juste et continue de parler.
        Assert.Contains(constats, f => f.Code.StartsWith("WARN-DOUBLON", StringComparison.Ordinal));
    }

    [Fact]
    public void Un_fichier_de_chainage_suspect_est_retire_du_controle_croise()
    {
        // C'est le coeur du defaut : sans ce retrait, chaque patient du RAA
        // ressort comme non chaine, une anomalie par ligne.
        var raa = Path.Combine(_dir, "er_psy_raa_2024_M12.txt");
        File.WriteAllLines(raa, Enumerable.Range(0, 40).Select(i =>
            new string('0', 21) + $"IPP-{i:D4}".PadRight(20) + new string(' ', 96 - 41)));

        var vh = Path.Combine(_dir, "er_psy_vh_2024_M12.txt");
        File.WriteAllLines(vh, Enumerable.Repeat(new string('0', 468), 40));

        var constats = new CheckRunner().Run(new[] { (raa, "RAA"), (vh, "VID-HOSP") });

        // Un seul constat de chainage, celui qui dit que la reference manque.
        Assert.Contains(constats, f => f.Code == "ERR-CHAINAGE-VID-ABSENT");
        Assert.DoesNotContain(constats, f => f.Code == "ERR-CHAINAGE-MANQUANT");
    }
}
