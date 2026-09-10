using System.Text;
using Pinel.Core.Formats;

namespace Pinel.Tests;

/// <summary>
/// Compares the embedded ATIH matrix against the official ATIH format
/// descriptors (workbooks "formats_*_2026.xlsx", downloaded 2026-08-28).
///
/// Why these tests exist. Until 2026-08-28 no official descriptor had ever
/// been read: the positions came from an earlier Python script. Two of them
/// turned out to be exactly right, and the rest were not. The worst case is
/// not an off-by-one, it is RPSA and R3A: those files carry an irreversible
/// hash of the IPP, and no date of birth at all. Reading bytes 22 to 41 of a
/// RPSA therefore stores the anonymisation key in the patient index, under
/// the label "IPP". That is precisely what MAGIC and PIVOINE exist to prevent.
/// </summary>
public sealed class AtihMatrixOfficielTests
{
    private static readonly Encoding Latin1 = Encoding.GetEncoding("ISO-8859-1");

    static AtihMatrixOfficielTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>
    /// Builds a synthetic fixed-width line: <paramref name="marks"/> maps a
    /// 1-indexed start position to the exact text to place there. Everything
    /// else is filler, so any field read outside a mark comes back as filler
    /// and is easy to recognise in an assertion.
    /// </summary>
    private static string Ligne(int length, params (int Start, string Text)[] marks)
    {
        var buffer = new StringBuilder(new string('.', length));
        foreach (var (start, text) in marks)
        {
            buffer.Remove(start - 1, text.Length).Insert(start - 1, text);
        }
        return buffer.ToString();
    }

    private static string EcrireLigne(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".txt");
        File.WriteAllText(path, content + Environment.NewLine, Latin1);
        return path;
    }

    // ---------------------------------------------------------------- RPS/RAA

    [Theory]
    [InlineData("RPS", 154)]
    [InlineData("RAA", 96)]
    public void Les_deux_formats_du_RIM_P_gardent_les_positions_officielles(string format, int longueur)
    {
        // Descriptif officiel ATIH 2026, feuilles "RPS" et "RAA" : identifiant
        // permanent du patient en 22-41, date de naissance en 42-49.
        // Ces deux formats sont le coeur du recueil et ils etaient DEJA justes.
        // Ce test existe pour qu'une correction des autres formats ne les casse
        // pas au passage : c'est le travail quotidien du DIM.
        var spec = AtihMatrix.All[format];

        Assert.Equal(longueur, spec.Length);
        Assert.Equal(21, spec.IppStart);
        Assert.Equal(41, spec.IppEnd);
        Assert.Equal(41, spec.DdnStart);
        Assert.Equal(49, spec.DdnEnd);
        Assert.True(spec.CarriesPatientIdentifiers);
    }

    [Fact]
    public void Un_RPS_reel_reste_decoupe_exactement_comme_avant()
    {
        // Non-regression de bout en bout sur le format le plus utilise.
        var ligne = Ligne(154, (22, "IPPFICTIF0000000001"), (42, "01011980"));
        var chemin = EcrireLigne(ligne);
        try
        {
            var record = new AtihParser().Parse(chemin, AtihMatrix.All["RPS"]).Single();
            Assert.Equal("IPPFICTIF0000000001.", record.Ipp);
            Assert.Equal("01011980", record.Ddn);
        }
        finally { File.Delete(chemin); }
    }

    // ------------------------------------------------- formats sans identifiant

    [Theory]
    [InlineData("RPSA")]        // sortie de PIVOINE : hachage irreversible en 25-40
    [InlineData("R3A")]         // idem, version ambulatoire
    [InlineData("FICHCOMP")]    // FICHCOMP transports : aucun identifiant, 63 caracteres
    [InlineData("FICHSUP-PSY")] // recueil agrege, supprime en psychiatrie depuis 2021
    public void Les_formats_sans_identifiant_officiel_sont_declares_comme_tels(string format)
    {
        // Etabli sur les descriptifs officiels 2026. Ces formats ne portent
        // AUCUN identifiant patient exploitable : soit rien du tout, soit le
        // resultat d'une anonymisation irreversible, qui n'est pas un IPP.
        Assert.False(
            AtihMatrix.All[format].CarriesPatientIdentifiers,
            $"{format} ne porte pas d'identifiant patient dans le descriptif officiel de l'ATIH.");
    }

    [Fact]
    public void Un_RPSA_ne_livre_plus_le_hachage_d_anonymisation_comme_un_IPP()
    {
        // LA vue rouge de ce lot. Un RPSA porte, en 25-40, le "cryptage
        // irreversible de l'IPP" produit par PIVOINE. En lisant 22-41, l'outil
        // recopiait ce hachage dans l'index patient sous le nom "IPP", et
        // lisait en 42-49 des octets qui ne sont pas une date de naissance.
        var ligne = Ligne(157, (25, "HACHAGEPIVOINE01"));
        var chemin = EcrireLigne(ligne);
        try
        {
            var records = new AtihParser().Parse(chemin, AtihMatrix.All["RPSA"]).ToList();

            Assert.Empty(records);
            Assert.DoesNotContain(records, r => r.Ipp.Contains("HACHAGE", StringComparison.Ordinal));
        }
        finally { File.Delete(chemin); }
    }

    // ------------------------------------------------------ positions corrigees

    [Theory]
    [InlineData("VID-HOSP", 520, 353, 373, 19, 27)]   // IPP 354-373, DDN 20-27
    [InlineData("RSFA", 257, 221, 241, 0, 0)]         // RSF A MCO : IPP 222-241, aucune DDN
    public void Les_positions_corrigees_suivent_le_descriptif_officiel(
        string format, int longueur, int ippStart, int ippEnd, int ddnStart, int ddnEnd)
    {
        var spec = AtihMatrix.All[format];
        Assert.Equal(longueur, spec.Length);
        Assert.Equal(ippStart, spec.IppStart);
        Assert.Equal(ippEnd, spec.IppEnd);
        Assert.Equal(ddnStart, spec.DdnStart);
        Assert.Equal(ddnEnd, spec.DdnEnd);
    }

    [Fact]
    public void EDGAR_n_est_plus_declare_comme_un_format_de_fichier()
    {
        // EDGAR est une typologie d'actes - entretien, demarche, groupe,
        // accompagnement, reunion - codee A L'INTERIEUR du RAA. Aucun fichier
        // national ne porte ce nom, il n'a donc rien a faire dans une matrice
        // de formats de fichiers.
        Assert.False(AtihMatrix.All.ContainsKey("EDGAR"));
    }
}
