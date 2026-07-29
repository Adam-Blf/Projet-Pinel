using Pinel.Core.Formats;
using Pinel.Core.Identity;
using Xunit;

namespace Pinel.Tests;

/// <summary>
/// End-to-end tests for positional extraction + MPI aggregation on
/// synthetic RPS lines.
/// </summary>
public sealed class AtihParserTests : IDisposable
{
    private readonly string _tempDir;

    public AtihParserTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "sovereign_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void Parse_extracts_ipp_and_ddn_from_rps_line()
    {
        // RPS format: length 154, IPP [21..41), DDN [41..49).
        var line = new string('0', 21) + "IPP-ABC-00012345678X" + "19900620" + new string(' ', 154 - 49);
        Assert.Equal(154, line.Length);
        var file = Path.Combine(_tempDir, "FV94_RPS_2024.txt");
        File.WriteAllText(file, line + Environment.NewLine);

        var records = new AtihParser().Parse(file).ToList();
        Assert.Single(records);
        Assert.Equal("IPP-ABC-00012345678X", records[0].Ipp);
        Assert.Equal("19900620", records[0].Ddn);
        Assert.Equal("RPS", records[0].Format);
    }

    [Fact]
    public void Parse_skips_short_and_padding_lines()
    {
        var good = new string('0', 21) + "IPP-ABC-00012345678X" + "19900620" + new string(' ', 154 - 49);
        var padding = new string('0', 154);
        var tooShort = "short";

        var file = Path.Combine(_tempDir, "FV94_RPS_2024.txt");
        File.WriteAllLines(file, new[] { good, padding, tooShort, good });

        var records = new AtihParser().Parse(file).ToList();
        Assert.Equal(2, records.Count);
    }

    [Fact]
    public void Mpi_detects_collisions_across_files()
    {
        var line1 = new string('0', 21) + "IPP-001-" + new string('A', 12) + "19900620" + new string(' ', 154 - 49);
        var line2 = new string('0', 21) + "IPP-001-" + new string('A', 12) + "19910620" + new string(' ', 154 - 49);

        var file1 = Path.Combine(_tempDir, "FV94_RPS_2023.txt");
        var file2 = Path.Combine(_tempDir, "FV94_RPS_2024.txt");
        File.WriteAllText(file1, line1 + Environment.NewLine);
        File.WriteAllText(file2, line2 + Environment.NewLine);

        var parser = new AtihParser();
        var mpi = new MasterPatientIndex();
        foreach (var record in parser.Parse(file1)) mpi.Add(record);
        foreach (var record in parser.Parse(file2)) mpi.Add(record);

        Assert.Single(mpi.All);
        Assert.Single(mpi.Collisions);
        Assert.Equal(2, mpi.All.First().DistinctDdnCount);
    }
}
