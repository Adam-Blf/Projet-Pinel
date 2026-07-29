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
}
