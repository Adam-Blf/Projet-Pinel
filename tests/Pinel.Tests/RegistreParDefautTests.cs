using Pinel.Core.Formats;

namespace Pinel.Tests;

/// <summary>
/// Le registre construit SANS argument est celui de la production
/// (<c>PinelSession</c>). Les autres tests du decoupage construisent le leur
/// avec <c>Array.Empty</c>, donc sans les descriptifs integres : ils prouvent la
/// garde A COTE du chemin reel. Ces tests-ci prennent le meme constructeur que
/// l'application.
/// </summary>
public sealed class RegistreParDefautTests
{
    /// <summary>
    /// Formats ou l'arrete du 23 decembre 2016 etablit qu'il n'y a ni
    /// identifiant patient ni date de naissance. Sortir deux colonnes a des
    /// positions devinees y ferait passer pour une donnee patient ce qui n'en
    /// est pas une.
    /// </summary>
    public static TheoryData<string> FormatsAnonymes()
    {
        var data = new TheoryData<string>();
        foreach (var (nom, spec) in AtihMatrix.All)
        {
            if (!spec.CarriesPatientIdentifiers) data.Add(nom);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(FormatsAnonymes))]
    public void Un_format_anonyme_ne_recoit_aucun_descriptif_par_defaut(string format)
    {
        var registre = new LayoutRegistry();

        // Resolve rend null, donc l'export bascule sur la colonne brute annoncee
        // au chapitre 4.3 du guide plutot que d'inventer un decoupage.
        Assert.Null(registre.Resolve(format, year: null));
    }

    [Fact]
    public void Au_moins_un_format_anonyme_est_declare()
    {
        // Sans cette borne, le theoreme ci-dessus passerait au vert sur un jeu
        // vide le jour ou quelqu'un retirerait la declaration.
        Assert.NotEmpty(FormatsAnonymes());
    }

    [Fact]
    public void Un_format_nominatif_recoit_l_identifiant_et_la_date_de_naissance()
    {
        var registre = new LayoutRegistry();
        var descriptif = registre.Resolve("RPS", year: null);

        Assert.NotNull(descriptif);
        Assert.Contains(descriptif!.Fields, f => f.Name == "IPP");
        Assert.Contains(descriptif.Fields, f => f.Name == "DATE_NAISSANCE");
    }

    [Fact]
    public void Aucun_descriptif_integre_ne_sort_un_champ_hors_de_la_ligne()
    {
        // Une position tiree d'un ancien script peut depasser la longueur
        // declaree du format. Le decoupage sortirait alors du vide, ou pire, le
        // debut du champ suivant.
        foreach (var descriptif in LayoutRegistry.BuiltIn())
        {
            var longueur = AtihMatrix.Require(descriptif.Format).Length;
            foreach (var champ in descriptif.Fields)
            {
                var fin = champ.Start - 1 + champ.Length;
                Assert.True(fin <= longueur,
                    $"{descriptif.Format}.{champ.Name} finit en {fin}, la ligne fait {longueur}");
            }
        }
    }
}
