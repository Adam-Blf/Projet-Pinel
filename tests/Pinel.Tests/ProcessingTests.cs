using Pinel.Core.Formats;
using Pinel.Core.Identity;
using Pinel.Core.Processing;
using Xunit;

namespace Pinel.Tests;

public sealed class ProcessingTests : IDisposable
{
    private readonly string _tempDir;

    public ProcessingTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "sovereign_proc_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
    }

    private string WriteRpsFile(string name, IEnumerable<(string ipp, string ddn)> rows)
    {
        var file = Path.Combine(_tempDir, name);
        using var w = new StreamWriter(file);
        foreach (var (ipp, ddn) in rows)
        {
            var line = new string('0', 21) + ipp.PadRight(20)[..20] + ddn.PadRight(8)[..8] + new string(' ', 154 - 49);
            w.WriteLine(line);
        }
        return file;
    }

    [Fact]
    public void Scanner_finds_all_txt_and_csv_recursively()
    {
        var nested = Path.Combine(_tempDir, "sub");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(_tempDir, "FV94_RPS_2024.txt"), "");
        File.WriteAllText(Path.Combine(nested, "FV94_RAA_2024.txt"), "");
        File.WriteAllText(Path.Combine(_tempDir, "unknown.csv"), "");

        var scanned = DirectoryScanner.Scan(new[] { _tempDir });
        Assert.Equal(3, scanned.Count);
        Assert.Contains(scanned, s => s.Format == "RPS");
        Assert.Contains(scanned, s => s.Format == "RAA");
        Assert.Contains(scanned, s => s.Format == "INCONNU");
    }

    [Fact]
    public void AtihProcessor_scan_and_process_builds_mpi()
    {
        WriteRpsFile("FV94_RPS_2024.txt", new[]
        {
            ("IPP-001", "19900101"),
            ("IPP-001", "19900101"),
            ("IPP-002", "19800505"),
        });

        var proc = new AtihProcessor();
        proc.AddFolders(new[] { _tempDir });
        var files = proc.Scan();
        Assert.Single(files);

        var totals = proc.ProcessAll();
        Assert.Equal(1, totals.FilesProcessed);
        Assert.Equal(2, totals.IppUnique);
        Assert.Equal(0, totals.Collisions);
    }

    [Fact]
    public async Task IdentityCsvExporter_writes_header_and_one_row_per_ipp()
    {
        WriteRpsFile("FV94_RPS_2024.txt", new[]
        {
            ("IPP-A", "19900620"),
            ("IPP-B", "19700101"),
        });

        var proc = new AtihProcessor();
        proc.AddFolders(new[] { _tempDir });
        proc.Scan();
        proc.ProcessAll();

        var output = Path.Combine(_tempDir, "mpi.csv");
        var written = await IdentityCsvExporter.ExportAsync(proc.Mpi, output);

        var lines = File.ReadAllLines(output);
        Assert.Equal(2, written);
        Assert.Equal(3, lines.Length); // header + 2 rows
        Assert.Contains("IPP;DDN_PIVOT", lines[0]);
    }
}
