using System.Collections.Concurrent;

namespace Pinel.Core.Logging;

/// <summary>
/// Thread-safe in-memory log buffer with a drain pattern: callers
/// (UI / frontend) pull log entries and clear the buffer in one atomic
/// call. Port of the Python <c>DataProcessor.logs</c> + <c>get_logs()</c>
/// used to stream progress to the pywebview frontend.
/// </summary>
public sealed class LogBuffer
{
    private readonly ConcurrentQueue<LogEntry> _queue = new();
    private readonly int _softCap;

    public LogBuffer(int softCap = 5000)
    {
        _softCap = softCap;
    }

    /// <summary>Number of entries currently buffered.</summary>
    public int Count => _queue.Count;

    /// <summary>Adds a message. Oldest entries are evicted beyond soft cap.</summary>
    public void Log(string message, LogLevel level = LogLevel.Info)
    {
        _queue.Enqueue(new LogEntry(
            Timestamp: DateTime.Now.ToString("HH:mm:ss.fff"),
            Level: level.ToString().ToUpperInvariant(),
            Message: message));

        while (_queue.Count > _softCap && _queue.TryDequeue(out _)) { /* evict */ }
    }

    /// <summary>
    /// Returns all buffered entries and empties the buffer atomically.
    /// Suitable for the bridge <c>/api/logs</c> endpoint.
    /// </summary>
    public IReadOnlyList<LogEntry> Drain()
    {
        var taken = new List<LogEntry>(_queue.Count);
        while (_queue.TryDequeue(out var entry)) taken.Add(entry);
        return taken;
    }
}

/// <summary>Severity of a log entry.</summary>
public enum LogLevel
{
    Debug,
    Info,
    Warn,
    Error,
}

/// <summary>One log entry serialisable to JSON for the frontend.</summary>
public sealed record LogEntry(string Timestamp, string Level, string Message);
