using System.Text.Json;
using Pinel.Core.Audit;
using Xunit;

namespace Pinel.Tests;

/// <summary>
/// Guards the redaction rule of the audit log: a FOLDER path is not patient
/// data and may be recorded, since it is what makes an act imputable; a patient
/// identifier never appears there in clear text. Also guards that the detail
/// field is actually filled, an empty one making the act untraceable.
/// </summary>
public sealed class RedactionAuditTests : IDisposable
{
    private readonly string _tempDir;

    public RedactionAuditTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "pinel_redaction_audit_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
    }

    private static string ReadFirstAuditLine(AuditLogger logger)
    {
        for (int i = 0; i < 50; i++)
        {
            if (File.Exists(logger.CurrentLogPath))
            {
                var lines = File.ReadAllLines(logger.CurrentLogPath);
                if (lines.Length > 0) return lines[0];
            }
            Thread.Sleep(50);
        }
        Assert.Fail("Aucune entrée d'audit écrite en 2,5 s");
        return string.Empty;
    }

    [Fact]
    public void Audit_detail_and_folder_are_recorded()
    {
        // A folder path is not patient data: it is what makes the act imputable.
        using var logger = new AuditLogger(Path.Combine(_tempDir, "audit_detail"));
        logger.Record("/api/export-csv", "POST", 200, 1024L,
            detail: "export CSV identités", folder: @"D:\PMSI\2024");

        var entry = JsonDocument.Parse(ReadFirstAuditLine(logger)).RootElement;
        Assert.Equal("export CSV identités", entry.GetProperty("detail").GetString());
        Assert.Equal(@"D:\PMSI\2024", entry.GetProperty("folder").GetString());
    }

    [Fact]
    public void Audit_detail_masks_a_patient_identifier()
    {
        using var logger = new AuditLogger(Path.Combine(_tempDir, "audit_mask"));
        logger.Record("/api/export-csv", "POST", 200, detail: "IPP 0000000000001 exporté");

        var line = ReadFirstAuditLine(logger);
        var detail = JsonDocument.Parse(line).RootElement.GetProperty("detail").GetString();

        Assert.DoesNotContain("0000000000001", line, StringComparison.Ordinal);
        Assert.Contains(AuditLogger.RedactionMarker, detail!, StringComparison.Ordinal);
    }

    [Fact]
    public void Audit_folder_keeps_a_short_year_but_masks_an_identifier()
    {
        using var logger = new AuditLogger(Path.Combine(_tempDir, "audit_folder"));
        logger.Record("/api/scanner", "POST", 200,
            folder: @"D:\PMSI\2024\lot-000000000000100");

        var folder = JsonDocument.Parse(ReadFirstAuditLine(logger)).RootElement
            .GetProperty("folder").GetString();

        Assert.Contains("2024", folder!, StringComparison.Ordinal);
        Assert.DoesNotContain("000000000000100", folder!, StringComparison.Ordinal);
    }

    [Fact]
    public void Audit_detail_is_never_left_null()
    {
        using var logger = new AuditLogger(Path.Combine(_tempDir, "audit_default"));
        logger.Record("/api/controles", "POST", 200);

        var detail = JsonDocument.Parse(ReadFirstAuditLine(logger)).RootElement.GetProperty("detail");
        Assert.Equal(JsonValueKind.String, detail.ValueKind);
        Assert.Equal(AuditLogger.UnspecifiedDetail, detail.GetString());
    }
}
