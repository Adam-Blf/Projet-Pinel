using Pinel.Core.Security;

namespace Pinel.Tests;

/// <summary>
/// Confinement des chemins. Les dossiers autorisés sont choisis par
/// l'utilisateur, y compris sur les partages du GHT, mais tout ce qui sort de
/// ces dossiers doit être refusé.
/// </summary>
public sealed class SafePathTests
{
    private static readonly string[] Roots =
    {
        @"C:\travail\pinel",
        @"O:\RIMP",
        @"\\serveur-dim\pmsi",
    };

    [Theory]
    [InlineData(@"C:\travail\pinel\lot\FV94_RPS_2025.txt")]
    [InlineData(@"O:\RIMP\2025\raa.txt")]
    [InlineData(@"\\serveur-dim\pmsi\2025\vidhosp.txt")]
    public void Accepte_les_chemins_sous_un_dossier_autorise(string candidate)
    {
        Assert.NotNull(SafePath.Resolve(candidate, Roots));
    }

    [Fact]
    public void Accepte_un_partage_reseau_declare()
    {
        var resolved = SafePath.Resolve(@"\\serveur-dim\pmsi\lot\raa.txt", Roots);
        Assert.Equal(@"\\serveur-dim\pmsi\lot\raa.txt", resolved);
    }

    [Theory]
    [InlineData(@"C:\Windows\System32\drivers\etc\hosts")]
    [InlineData(@"\\autre-serveur\partage\lot.txt")]
    [InlineData(@"D:\ailleurs\lot.txt")]
    public void Refuse_les_chemins_hors_dossiers_autorises(string candidate)
    {
        Assert.Null(SafePath.Resolve(candidate, Roots));
    }

    [Fact]
    public void Refuse_la_remontee_par_double_point()
    {
        Assert.Null(SafePath.Resolve(@"C:\travail\pinel\..\..\secrets.txt", Roots));
    }

    [Fact]
    public void Refuse_un_flux_de_donnees_alterne()
    {
        Assert.Null(SafePath.Resolve(@"C:\travail\pinel\lot.txt:cache", Roots));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Refuse_un_chemin_vide(string candidate)
    {
        Assert.Null(SafePath.Resolve(candidate, Roots));
    }

    [Fact]
    public void Le_dossier_autorise_lui_meme_est_accepte()
    {
        Assert.NotNull(SafePath.Resolve(@"O:\RIMP", Roots));
    }

    [Fact]
    public void Un_dossier_de_nom_proche_est_refuse()
    {
        // O:\RIMP2 ne doit pas passer parce qu'il commence comme O:\RIMP.
        Assert.Null(SafePath.Resolve(@"O:\RIMP2\lot.txt", Roots));
    }

    // Défaut 2 (audit) : SafePath.Resolve testait le préfixe de périphérique
    // Win32 (\\?\ ou \\.\) sur la chaîne BRUTE, avant Path.GetFullPath. Une
    // graphie en barres obliques ("//?/...") échappe à un test sur la chaîne
    // brute, alors que Path.GetFullPath lui appose le même préfixe une fois
    // normalisée. Le résultat final (refus) ne changeait pas - la
    // comparaison de préfixe de la racine autorisée fermait quand même la
    // porte - mais la garde ne faisait pas ce qu'elle annonçait. Les quatre
    // cas ci-dessous doivent tous rester refusés avant et après correction ;
    // le test blanc-boîte suivant (Resolve_teste_le_prefixe_etendu_sur_le_chemin_normalise)
    // prouve en plus QUE la vérification porte bien sur le chemin normalisé.
    [Theory]
    [InlineData("//?/C:/travail/pinel/../../secrets.txt", "prefixe étendu en barres obliques")]
    [InlineData(@"\\?\C:\travail\pinel\..\..\secrets.txt", "forme avec point (traversée ..) sous préfixe étendu")]
    [InlineData(@"C:\", "racine de volume nue")]
    [InlineData(@"\\serveur-dim", "racine UNC nue")]
    public void Reste_refuse_avant_et_apres_correction_du_prefixe_etendu(string candidate, string label)
    {
        Assert.Null(SafePath.Resolve(candidate, Roots));
    }

    [Fact]
    public void Resolve_teste_le_prefixe_etendu_sur_le_chemin_normalise_pas_sur_la_chaine_brute()
    {
        // Caractérise le défaut lui-même : la chaîne brute en barres obliques
        // ne porte pas le préfixe Win32, seul le chemin normalisé le porte.
        const string candidate = "//?/C:/travail/pinel/../../secrets.txt";
        Assert.False(candidate.StartsWith(@"\\?\", StringComparison.Ordinal));

        var normalized = Path.GetFullPath(candidate);
        Assert.True(normalized.StartsWith(@"\\?\", StringComparison.Ordinal));

        // Preuve directe de la correction : SafePath.IsExtendedDevicePath
        // n'existe (et n'est appelé sur le chemin normalisé) qu'après le
        // correctif. Avant, ce test ne compile pas - c'est la vue rouge.
        Assert.True(SafePath.IsExtendedDevicePath(normalized));
        Assert.False(SafePath.IsExtendedDevicePath(candidate));
    }
}
