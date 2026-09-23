using Pinel.Core.Checks;
using Pinel.Core.Formats;

namespace Pinel.Tests;

/// <summary>
/// Controles corriges apres leur passage sur un lot reel 2026 accepte par
/// e-PMSI (22/09/2026) : chacun levait des milliers d'anomalies fausses.
/// Toutes les lignes sont synthetiques.
/// </summary>
public sealed class ChecksLotReelTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pinel-lotreel-" + Guid.NewGuid().ToString("N"));

    public ChecksLotReelTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private static string Line(int length, params (int Offset, string Value)[] fields)
    {
        var chars = new char[length];
        Array.Fill(chars, ' ');
        foreach (var (offset, value) in fields) value.CopyTo(0, chars, offset, value.Length);
        return new string(chars);
    }

    private string Write(string name, params string[] lines)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllLines(path, lines, PmsiEncoding.Latin1);
        return path;
    }

    [Fact]
    public void Ddn_au_format_officiel_jjmmaaaa_est_acceptee()
    {
        var rps = AtihMatrix.Require("RPS");
        var file = Write("rps.txt", Line(rps.Length, (0, "940140049"), (rps.DdnStart, "25121990")));

        Assert.Empty(new BirthDateFormatCheck().Validate(file, "RPS"));
    }

    [Fact]
    public void Ddn_inversee_aaaammjj_est_signalee()
    {
        var rps = AtihMatrix.Require("RPS");
        var file = Write("rps.txt", Line(rps.Length, (0, "940140049"), (rps.DdnStart, "19901225")));

        Assert.Contains(new BirthDateFormatCheck().Validate(file, "RPS"), f => f.Code == "WARN-DDN-AAAAMMJJ");
    }

    [Fact]
    public void Raa_chaine_par_le_vid_ipp_n_est_pas_signale()
    {
        var raa = AtihMatrix.Require("RAA");
        var vidHosp = AtihMatrix.Require("VID-HOSP");
        var vidIpp = AtihMatrix.Require("VID-IPP");
        var files = new List<(string, string)>
        {
            (Write("raa.txt", Line(raa.Length, (raa.IppStart, "IPPAMBU01"))), "RAA"),
            (Write("vh.txt", Line(vidHosp.Length, (vidHosp.IppStart, "IPPHOSP01"))), "VID-HOSP"),
            (Write("vipp.txt", Line(vidIpp.Length, (vidIpp.IppStart, "IPPAMBU01"))), "VID-IPP"),
        };

        var findings = new ChainageCoverageCheck().Validate(files).ToList();
        Assert.DoesNotContain(findings, f => f.Code == "ERR-CHAINAGE-MANQUANT");
    }

    [Fact]
    public void Patient_non_chaine_ne_produit_qu_une_anomalie()
    {
        var raa = AtihMatrix.Require("RAA");
        var vidHosp = AtihMatrix.Require("VID-HOSP");
        var line = Line(raa.Length, (raa.IppStart, "IPPABSENT1"));
        var files = new List<(string, string)>
        {
            (Write("raa.txt", line, line, line, line), "RAA"),
            (Write("vh.txt", Line(vidHosp.Length, (vidHosp.IppStart, "IPPHOSP01"))), "VID-HOSP"),
        };

        var findings = new ChainageCoverageCheck().Validate(files).Where(f => f.Code == "ERR-CHAINAGE-MANQUANT").ToList();
        Assert.Single(findings);
        Assert.Equal(1, findings[0].LineNumber);
    }

    [Fact]
    public void Finess_du_vid_hosp_est_lu_en_53_et_non_sur_le_nir()
    {
        var vidHosp = AtihMatrix.Require("VID-HOSP");
        // NIR alphanumerique en tete (position 1), FINESS valide en 53.
        var file = Write("vh.txt", Line(vidHosp.Length, (0, "2A0123456789"), (52, "940140049")));

        Assert.DoesNotContain(new FinessCheck().Validate(file, "VID-HOSP"), f => f.Code == "ERR-FINESS-FORMAT");

        var bad = Write("vh2.txt", Line(vidHosp.Length, (52, "94014A049")));
        Assert.Contains(new FinessCheck().Validate(bad, "VID-HOSP"), f => f.Code == "ERR-FINESS-FORMAT");
    }
}
