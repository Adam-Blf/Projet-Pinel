using Pinel.Core.Logging;
using Pinel.Core.Processing;
using Xunit;

namespace Pinel.Tests;

public sealed class LoggingAndInspectionTests : IDisposable
{
    private readonly string _tempDir;

    public LoggingAndInspectionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "sovereign_log_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void LogBuffer_drain_returns_all_entries_and_clears()
    {
        var buf = new LogBuffer();
        buf.Log("a");
        buf.Log("b", LogLevel.Warn);
        buf.Log("c", LogLevel.Error);

        var drained = buf.Drain();
        Assert.Equal(3, drained.Count);
        Assert.Equal("WARN", drained[1].Level);
        Assert.Empty(buf.Drain());
    }

    [Fact]
    public void LogBuffer_enforces_soft_cap_by_evicting_oldest()
    {
        var buf = new LogBuffer(softCap: 3);
        for (int i = 0; i < 10; i++) buf.Log("msg-" + i);
        var drained = buf.Drain();
        Assert.Equal(3, drained.Count);
        Assert.Equal("msg-9", drained[^1].Message);
    }

    [Fact]
    public void Inspector_returns_rps_fields()
    {
        var file = Path.Combine(_tempDir, "FV94_RPS_2024.txt");
        // Build one RPS line with FINESS at 0-9, IPP at 21-41, DDN at 41-49, DP at 140-148.
        var sb = new System.Text.StringBuilder(new string('0', 154));
        var finess = "123456789";
        for (int i = 0; i < finess.Length; i++) sb[i] = finess[i];
        var ipp = "IPP-AAA-12345678900X";
        for (int i = 0; i < ipp.Length; i++) sb[21 + i] = ipp[i];
        var ddn = "19900620";
        for (int i = 0; i < ddn.Length; i++) sb[41 + i] = ddn[i];
        var dp = "F200    ";
        for (int i = 0; i < dp.Length; i++) sb[140 + i] = dp[i];
        File.WriteAllLines(file, new[] { sb.ToString() });

        var result = LineInspector.Inspect(file, 1);
        Assert.NotNull(result);
        Assert.Equal("RPS", result.Format);
        Assert.Equal("IPP-AAA-12345678900X", result.Fields["ipp"]);
        Assert.Equal("19900620", result.Fields["ddn"]);
        Assert.Equal("123456789", result.Fields["finess_juridique"]);
        Assert.Contains("F20", result.Fields["diagnostic_principal"]);
    }

    [Fact]
    public void Inspector_out_of_range_returns_null()
    {
        var file = Path.Combine(_tempDir, "FV94_RPS_2024.txt");
        File.WriteAllLines(file, new[] { new string('0', 154) });
        Assert.Null(LineInspector.Inspect(file, 5));
    }

    [Fact]
    public void Inspector_unknown_format_returns_empty_fields()
    {
        var file = Path.Combine(_tempDir, "mystery.txt");
        File.WriteAllLines(file, new[] { "some data" });
        var result = LineInspector.Inspect(file, 1);
        Assert.NotNull(result);
        Assert.Null(result.Format);
        Assert.Empty(result.Fields);
    }
}
