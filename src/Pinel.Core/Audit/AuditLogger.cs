using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace Pinel.Core.Audit;

/// <summary>
/// Append-only audit log required by RGPD article 30 (registre des
/// activités de traitement). Captures the WHO/WHEN/WHAT of every
/// sensitive bridge operation without ever recording patient data.
/// </summary>
/// <remarks>
/// File - <c>%LOCALAPPDATA%\Pinel\audit.log</c>.
/// Format - JSON Lines (one record per line, UTF-8 sans BOM).
/// Rotation - daily - filename postfixed with <c>-YYYYMMDD</c>.
/// Thread-safe - writes are serialised through a <see cref="Channel"/>-backed
/// queue and flushed from a single background task.
/// </remarks>
public sealed class AuditLogger : IDisposable
{
    private static readonly object SingletonLock = new();
    private static AuditLogger? _instance;

    private readonly string _directory;
    private readonly BlockingCollection<string> _queue = new(new ConcurrentQueue<string>());
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _worker;

    /// <summary>
    /// Public constructor for tests and DI. Production code should prefer
    /// <see cref="Instance"/> which points at <c>%LOCALAPPDATA%\Pinel</c>.
    /// </summary>
    public AuditLogger(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
        _worker = Task.Run(WriteLoopAsync);
    }

    /// <summary>Returns the process-wide instance (lazily initialised).</summary>
    public static AuditLogger Instance
    {
        get
        {
            if (_instance is not null) return _instance;
            lock (SingletonLock)
            {
                return _instance ??= new AuditLogger(DefaultDirectory());
            }
        }
    }

    private static string DefaultDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Pinel");

    /// <summary>
    /// Record a sensitive operation. Patient-identifying data must never
    /// be passed in <paramref name="detail"/> - restrict to endpoint,
    /// HTTP method, status code, byte count, generic labels.
    /// </summary>
    public void Record(string endpoint, string method, int status, long? bytes = null, string? detail = null)
    {
        var payload = new
        {
            ts = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            host = Environment.MachineName,
            user = Environment.UserName,
            endpoint,
            method,
            status,
            bytes,
            detail,
        };
        var line = System.Text.Json.JsonSerializer.Serialize(payload);
        _queue.TryAdd(line); // non-blocking; lossless unless shutdown
    }

    public string CurrentLogPath => Path.Combine(
        _directory,
        $"audit-{DateTime.Now:yyyyMMdd}.log");

    private async Task WriteLoopAsync()
    {
        try
        {
            foreach (var line in _queue.GetConsumingEnumerable(_cts.Token))
            {
                try
                {
                    await File.AppendAllTextAsync(CurrentLogPath, line + Environment.NewLine);
                }
                catch (IOException)
                {
                    // Best-effort - never block the app on audit IO.
                }
            }
        }
        catch (OperationCanceledException)
        {
            /* shutdown */
        }
    }

    public void Dispose()
    {
        _queue.CompleteAdding();
        _cts.Cancel();
        try { _worker.Wait(TimeSpan.FromSeconds(1)); } catch { /* best effort */ }
        _queue.Dispose();
        _cts.Dispose();
    }
}
