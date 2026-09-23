using Pinel.Core.Structure;
using Xunit;

namespace Pinel.Tests;

/// <summary>
/// Le fichier de structure tel que le service le tient réellement, relevé le
/// 23/09/2026 : 272 unités médicales, deux FINESS d'inscription, soixante-quatre
/// FINESS géographiques. Ses intitulés de colonnes ne sont pas ceux que le
/// parseur attendait.
/// </summary>
public sealed class StructureReelleTests : IDisposable
{
    private readonly string _dir;

    public StructureReelleTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "pinel-structure-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    /// <summary>
    /// En-têtes et valeurs de la même forme que le fichier réel. Les codes
    /// d'unité et les libellés sont inventés, les intitulés de colonnes non :
    /// ce sont eux qui sont testés.
    /// </summary>
    private string EcrireStructureReelle()
    {
        var chemin = Path.Combine(_dir, "structure.csv");
        File.WriteAllLines(chemin, new[]
        {
            "site_extract;finess_epmsi;finess_geo;UM;lib_um;um_num;code_site;lib_um_court",
            "ER;000000000;000000001;4311;TEMPS PLEIN UIR;4311;2;HTPlein UIR",
            "ER;000000000;000000001;4312;TEMPS PLEIN UPG;4312;2;HTPlein UPG",
            "PG_FV;000000002;000000003;0174;PARTIEL INFANTO;0174;1;Partiel IJ",
        });
        return chemin;
    }

    [Fact]
    public void Le_libelle_de_l_unite_est_lu()
    {
        // La colonne s'appelle lib_um dans le fichier du service, pas libelle.
        // Sans cet alias, l'arbre sortait avec des noeuds sans nom.
        var resultat = StructureParser.Parse(EcrireStructureReelle());

        Assert.Equal(3, resultat.Tree.Count);
        Assert.All(resultat.Tree, n => Assert.False(string.IsNullOrWhiteSpace(n.Label)));
        Assert.Contains(resultat.Tree, n => n.Label.Contains("TEMPS PLEIN UIR", StringComparison.Ordinal));
    }

    [Fact]
    public void Le_code_de_l_unite_est_lu()
    {
        var resultat = StructureParser.Parse(EcrireStructureReelle());

        Assert.Contains(resultat.Tree, n => n.Code == "4311");
        Assert.Contains(resultat.Tree, n => n.Code == "0174");
    }

    [Fact]
    public void Un_libelle_court_ne_prend_pas_la_place_du_libelle()
    {
        // lib_um et lib_um_court coexistent dans le fichier reel. C'est le
        // premier qui porte le libelle complet, et l'ordre des colonnes ne
        // doit pas decider a sa place.
        var chemin = Path.Combine(_dir, "inverse.csv");
        File.WriteAllLines(chemin, new[]
        {
            "UM;lib_um_court;lib_um",
            "4311;HTPlein UIR;TEMPS PLEIN UIR",
        });

        var resultat = StructureParser.Parse(chemin);

        Assert.Equal("TEMPS PLEIN UIR", resultat.Tree[0].Label);
    }

    [Fact]
    public void Un_code_de_secteur_reel_est_classe()
    {
        // Codes releves dans la feuille um_bis du fichier du service.
        Assert.Equal("I", StructureParser.DetectSectorType("92I07"));
        Assert.Equal("G", StructureParser.DetectSectorType("94G13"));
    }
}
