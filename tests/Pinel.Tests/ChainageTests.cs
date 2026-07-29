using Pinel.Core.Checks;
using Xunit;

namespace Pinel.Tests;

public sealed class ChainageTests : IDisposable
{
    private readonly string _tempDir;

    public ChainageTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "sovereign_chain_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
    }

    private string WriteRps(string ipp, string ddn = "19900620")
    {
        var file = Path.Combine(_tempDir, $"FV94_RPS_2024_{Guid.NewGuid():N}.txt");
        var line = new string('0', 21) + ipp.PadRight(20)[..20] + ddn + new string(' ', 154 - 49);
        File.WriteAllLines(file, new[] { line });
        return file;
    }

    private string WriteVidHosp(params string[] ipps)
    {
        var file = Path.Combine(_tempDir, "FV94_VID-HOSP_2024.txt");
        var lines = ipps.Select(ipp =>
        {
            // VID-HOSP length 518, IPP [265..285), DDN [19..27)
            var sb = new System.Text.StringBuilder(new string(' ', 518));
            var paddedIpp = ipp.PadRight(20)[..20];
            for (int i = 0; i < paddedIpp.Length; i++) sb[265 + i] = paddedIpp[i];
            // Put a valid-looking NIR at the start so NIR validator stays happy.
            var nir = "199046234567891"; // 13-digit NIR + 2-digit key
            for (int i = 0; i < nir.Length && i < 15; i++) sb[i] = nir[i];
            return sb.ToString();
        }).ToList();
        File.WriteAllLines(file, lines);
        return file;
    }

    [Fact]
    public void Chainage_flags_rps_ipp_missing_from_vid_hosp()
    {
        var rpsA = WriteRps("IPP-A-00000000000000");
        var rpsB = WriteRps("IPP-B-00000000000000");
        var vid = WriteVidHosp("IPP-A-00000000000000"); // B is missing

        var validator = new ChainageCoverageCheck();
        var findings = validator.Validate(new[]
        {
            (Path: rpsA, Format: "RPS"),
            (Path: rpsB, Format: "RPS"),
            (Path: vid, Format: "VID-HOSP"),
        }).ToList();

        Assert.Contains(findings, f => f.Code == "ERR-CHAINAGE-MANQUANT" && f.Message.Contains("IPP-B"));
    }

    [Fact]
    public void Chainage_flags_orphan_vid_hosp_ipp()
    {
        var rps = WriteRps("IPP-A-00000000000000");
        var vid = WriteVidHosp("IPP-A-00000000000000", "IPP-Z-ORPHAN0000000000");

        var findings = new ChainageCoverageCheck().Validate(new[]
        {
            (Path: rps, Format: "RPS"),
            (Path: vid, Format: "VID-HOSP"),
        }).ToList();

        Assert.Contains(findings, f => f.Code == "WARN-CHAINAGE-ORPHAN");
    }

    [Fact]
    public void Chainage_flags_missing_vid_hosp_batch()
    {
        var rps = WriteRps("IPP-A-00000000000000");
        var findings = new ChainageCoverageCheck().Validate(new[]
        {
            (Path: rps, Format: "RPS"),
        }).ToList();

        Assert.Contains(findings, f => f.Code == "ERR-CHAINAGE-VID-ABSENT");
    }

    [Fact]
    public void Nir_missing_is_flagged()
    {
        var file = Path.Combine(_tempDir, "FV94_VID-HOSP_2024.txt");
        // NIR all zeros.
        var line = new string('0', 15) + new string(' ', 518 - 15);
        File.WriteAllLines(file, new[] { line });

        var findings = new ChainageNirCheck().Validate(file, "VID-HOSP").ToList();
        Assert.Contains(findings, f => f.Code == "ERR-VID-NIR-MISSING");
    }

    [Fact]
    public void Nir_non_numeric_is_error()
    {
        var file = Path.Combine(_tempDir, "FV94_VID-HOSP_2024.txt");
        var line = "ABCDEFGHIJKLM" + "45" + new string(' ', 518 - 15);
        File.WriteAllLines(file, new[] { line });

        var findings = new ChainageNirCheck().Validate(file, "VID-HOSP").ToList();
        Assert.Contains(findings, f => f.Code == "ERR-VID-NIR-FORMAT");
    }
}
