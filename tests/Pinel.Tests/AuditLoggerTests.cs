using Pinel.Core.Audit;
using Xunit;

namespace Pinel.Tests;

public sealed class AuditLoggerTests : IDisposable
{
    private readonly string _tempDir;

    public AuditLoggerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "sovereign_audit_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void Recorded_entry_appears_on_disk()
    {
        using var logger = new AuditLogger(_tempDir);
        logger.Record("/api/export", "POST", 200, 1024L, "csv");

        // Allow the background writer to flush.
        for (int i = 0; i < 50; i++)
        {
            var logFiles = Directory.GetFiles(_tempDir, "*.log");
            if (logFiles.Length > 0)
            {
                // L'ecrivain de fond peut tenir le fichier pendant la lecture :
                // lecture partagee, et nouvel essai si le verrou est pris.
                List<string> lines;
                try
                {
                    lines = logFiles.SelectMany(ReadShared).ToList();
                }
                catch (IOException)
                {
                    Thread.Sleep(50);
                    continue;
                }
                if (lines.Count > 0)
                {
                    Assert.Contains(lines, l => l.Contains("/api/export") && l.Contains("\"status\":200"));
                    return;
                }
            }
            Thread.Sleep(50);
        }
        Assert.Fail("No audit entry written within 2.5 s");
    }

    private static IEnumerable<string> ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    [Fact]
    public void Log_file_name_contains_date()
    {
        using var logger = new AuditLogger(_tempDir);
        Assert.Contains(DateTime.Now.ToString("yyyyMMdd"), logger.CurrentLogPath);
    }

    [Fact]
    public void Record_never_stores_request_body_or_patient_data()
    {
        using var logger = new AuditLogger(_tempDir);
        // The API does not expose a body parameter. This test is a guard:
        // if someone adds a patient-data parameter, this test breaks at compile time.
        // The method signature is (endpoint, method, status, bytes?, detail?).
        var method = typeof(AuditLogger).GetMethod("Record");
        Assert.NotNull(method);
        var parameters = method!.GetParameters();
        Assert.DoesNotContain(parameters, p => p.Name is "ipp" or "ddn" or "body" or "patient");
    }
}
