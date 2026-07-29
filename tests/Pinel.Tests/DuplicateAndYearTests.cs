using Pinel.Core.Checks;
using Xunit;

namespace Pinel.Tests;

public sealed class DoublonsEtAnneeTests : IDisposable
{
    private readonly string _tempDir;

    public DoublonsEtAnneeTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "sovereign_dupyear_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
    }

    private string BuildRps(string ipp, string ddn)
    {
        var line = new string('0', 21) + ipp.PadRight(20)[..20] + ddn.PadRight(8)[..8] + new string(' ', 154 - 49);
        return line;
    }

    [Fact]
    public void Duplicate_line_is_flagged_once()
    {
        var line = BuildRps("IPP-AAA", "19900101");
        var file = Path.Combine(_tempDir, "FV94_RPS_2024.txt");
        File.WriteAllLines(file, new[] { line, line, line });

        var findings = new DuplicateLineCheck().Validate(file, "RPS").ToList();
        // Two duplicates (lines 2 and 3 against line 1).
        Assert.Equal(2, findings.Count(f => f.Code == "WARN-DOUBLON-LIGNE"));
    }

    [Fact]
    public void Distinct_lines_produce_no_findings()
    {
        var a = BuildRps("IPP-AAA", "19900101");
        var b = BuildRps("IPP-BBB", "19800505");
        var file = Path.Combine(_tempDir, "FV94_RPS_2024.txt");
        File.WriteAllLines(file, new[] { a, b });

        var findings = new DuplicateLineCheck().Validate(file, "RPS").ToList();
        Assert.DoesNotContain(findings, f => f.Code == "WARN-DOUBLON-LIGNE");
    }

    [Fact]
    public void Bulk_duplicates_trigger_error_summary()
    {
        var line = BuildRps("IPP-AAA", "19900101");
        var file = Path.Combine(_tempDir, "FV94_RPS_2024.txt");
        // 1 original + 25 duplicates.
        File.WriteAllLines(file, Enumerable.Repeat(line, 26));

        var findings = new DuplicateLineCheck().Validate(file, "RPS").ToList();
        Assert.Contains(findings, f => f.Code == "WARN-DOUBLON-BULK");
    }

    [Fact]
    public void Ddn_posterior_to_file_year_is_flagged()
    {
        var file = Path.Combine(_tempDir, "FV94_RPS_2020.txt");
        // DDN in 2024, file name says 2020 - clear mismatch.
        var line = BuildRps("IPP-A", "20240101");
        File.WriteAllLines(file, new[] { line });

        var findings = new FileYearCheck().Validate(file, "RPS").ToList();
        Assert.Contains(findings, f => f.Code == "WARN-DDN-APRES-ANNEE-FICHIER");
    }

    [Fact]
    public void Filename_without_year_produces_no_findings()
    {
        var file = Path.Combine(_tempDir, "unknown.txt");
        var line = BuildRps("IPP-A", "19900101");
        File.WriteAllLines(file, new[] { line });
        var findings = new FileYearCheck().Validate(file, "RPS").ToList();
        Assert.DoesNotContain(findings, f => f.Code == "WARN-DDN-APRES-ANNEE-FICHIER");
    }

    [Fact]
    public void Future_year_in_filename_is_warning()
    {
        var file = Path.Combine(_tempDir, "FV94_RPS_2099.txt");
        var line = BuildRps("IPP-A", "19900101");
        File.WriteAllLines(file, new[] { line });
        var findings = new FileYearCheck().Validate(file, "RPS").ToList();
        Assert.Contains(findings, f => f.Code == "WARN-FILE-YEAR-FUTURE");
    }
}
