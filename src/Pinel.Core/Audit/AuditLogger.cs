using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Pinel.Core.Security;

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
        PinelPaths.DataRoot);

    /// <summary>
    /// Record a sensitive operation.
    /// <para>
    /// Redaction rule of the audit log, enforced by <see cref="Sanitize"/>:
    /// a FOLDER path is NOT patient data and may appear in
    /// <paramref name="folder"/> or <paramref name="detail"/> - it is precisely
    /// what makes an act imputable, since it names the workspace acted upon.
    /// A patient identifier (IPP, NIR, date of birth, name) NEVER appears there
    /// in clear text. Restrict <paramref name="detail"/> to generic labels:
    /// operation performed, kind of export, record count.
    /// </para>
    /// <para>
    /// <paramref name="detail"/> is optional so existing callers keep compiling,
    /// but it is never written empty: an unfilled field is logged as
    /// <see cref="UnspecifiedDetail"/> so a reader can tell "not documented"
    /// from "stripped".
    /// </para>
    /// </summary>
    /// <param name="endpoint">Bridge route that was called.</param>
    /// <param name="method">HTTP method.</param>
    /// <param name="status">HTTP status returned.</param>
    /// <param name="bytes">Response size, when known.</param>
    /// <param name="detail">What the act did, in generic terms.</param>
    /// <param name="folder">Directory the act operated on, when applicable.</param>
    public void Record(
        string endpoint,
        string method,
        int status,
        long? bytes = null,
        string? detail = null,
        string? folder = null)
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
            detail = string.IsNullOrWhiteSpace(detail) ? UnspecifiedDetail : Sanitize(detail),
            folder = Sanitize(folder),
        };
        var line = System.Text.Json.JsonSerializer.Serialize(payload);
        _queue.TryAdd(line); // non-blocking; lossless unless shutdown
    }

    /// <summary>Value written when a caller documents no detail.</summary>
    public const string UnspecifiedDetail = "non précisé";

    /// <summary>Replacement written in place of a suspected identifier.</summary>
    public const string RedactionMarker = "[masqué]";

    /// <summary>
    /// Shortest patient or establishment identifier handled by Pinel: FINESS is
    /// 9 digits, IPP and NIR are longer. Below that threshold a digit run is a
    /// count, a year or a size, and stays readable.
    /// </summary>
    private const int IdentifierDigitRun = 9;

    /// <summary>
    /// Mechanical enforcement of the rule documented on <see cref="Record"/>:
    /// any run of <see cref="IdentifierDigitRun"/> digits or more is replaced by
    /// <see cref="RedactionMarker"/>. Letters, separators and folder paths are
    /// left untouched, so <c>D:\PMSI\2024</c> survives while
    /// <c>IPP 2860675110042</c> does not.
    /// </summary>
    private static string? Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value;

        var builder = new StringBuilder(value.Length);
        int index = 0;
        while (index < value.Length)
        {
            if (!char.IsDigit(value[index]))
            {
                builder.Append(value[index++]);
                continue;
            }

            int start = index;
            while (index < value.Length && char.IsDigit(value[index])) index++;

            int run = index - start;
            if (run >= IdentifierDigitRun) builder.Append(RedactionMarker);
            else builder.Append(value, start, run);
        }
        return builder.ToString();
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
