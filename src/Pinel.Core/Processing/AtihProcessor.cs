using System.Collections.Concurrent;
using Pinel.Core.Formats;
using Pinel.Core.Identity;
using Pinel.Core.Models;

namespace Pinel.Core.Processing;

/// <summary>
/// Stateful orchestrator: scans, parses, builds MPI, exposes stats.
/// Port of <c>DataProcessor</c> in Python (without PDF/CSV export, which
/// live in their own classes for single-responsibility).
/// </summary>
public sealed class AtihProcessor
{
    private readonly AtihParser _parser = new();
    private readonly List<string> _folders = new();
    private readonly List<ScannedFile> _files = new();
    private readonly MasterPatientIndex _mpi = new();
    private readonly ConcurrentDictionary<string, FileStats> _fileStats = new();

    /// <summary>Folders currently registered by the user.</summary>
    public IReadOnlyList<string> Folders => _folders;

    /// <summary>Files produced by the most recent <see cref="Scan"/>.</summary>
    public IReadOnlyList<ScannedFile> Files => _files;

    /// <summary>Master Patient Index aggregated across all processed files.</summary>
    public MasterPatientIndex Mpi => _mpi;

    /// <summary>Per-file statistics keyed by file name.</summary>
    public IReadOnlyDictionary<string, FileStats> FileStats => _fileStats;

    /// <summary>
    /// Adds the given folders to the current working set. Non-existent
    /// folders and duplicates are silently skipped.
    /// </summary>
    public void AddFolders(IEnumerable<string> folders)
    {
        foreach (var folder in folders)
        {
            if (!Directory.Exists(folder)) continue;
            if (!_folders.Contains(folder, StringComparer.OrdinalIgnoreCase))
            {
                _folders.Add(folder);
            }
        }
    }

    /// <summary>Removes all folders and scanned-file metadata. MPI preserved.</summary>
    public void ClearFolders()
    {
        _folders.Clear();
        _files.Clear();
    }

    /// <summary>
    /// Detecteur par le contenu, pose par la session a partir des descriptifs
    /// deposes. Tant qu'il est absent, le scan retombe sur le nom du fichier.
    /// </summary>
    public ContentFormatDetector? Detector { get; set; }

    /// <summary>
    /// Recursively scans every registered folder for ATIH candidate files
    /// and replaces <see cref="Files"/> with the result.
    /// </summary>
    public IReadOnlyList<ScannedFile> Scan()
    {
        _files.Clear();
        _files.AddRange(DirectoryScanner.Scan(_folders, Detector));
        return _files;
    }

    /// <summary>
    /// Parses every scanned file in parallel and feeds the MPI. Returns a
    /// totals summary matching the Python <c>process_files()</c> shape.
    /// </summary>
    public ProcessingTotals ProcessAll(int maxDegreeOfParallelism = 8)
    {
        var totals = new ProcessingTotals();
        var lockObj = new object();

        Parallel.ForEach(
            _files,
            new ParallelOptions { MaxDegreeOfParallelism = Math.Clamp(maxDegreeOfParallelism, 1, 16) },
            file =>
            {
                if (file.Format == "INCONNU" || !AtihMatrix.All.ContainsKey(file.Format))
                {
                    Interlocked.Increment(ref totals.FilesSkippedRaw);
                    return;
                }

                var format = AtihMatrix.Require(file.Format);
                var stats = new FileStats(file.Format);
                var localRecords = new List<PatientRecord>();

                try
                {
                    foreach (var record in _parser.Parse(file.Path, format))
                    {
                        localRecords.Add(record);
                        stats.LinesValid++;
                    }
                }
                catch (IOException)
                {
                    Interlocked.Increment(ref totals.FilesSkippedRaw);
                    return;
                }

                _fileStats[file.Name] = stats;
                Interlocked.Increment(ref totals.FilesProcessedRaw);
                Interlocked.Add(ref totals.LinesValidRaw, stats.LinesValid);

                lock (lockObj)
                {
                    foreach (var r in localRecords) _mpi.Add(r);
                }
            });

        totals.IppUnique = _mpi.Count;
        totals.Collisions = _mpi.Collisions.Count();
        return totals;
    }

    /// <summary>
    /// Full state reset: clears folders, files, per-file stats, and the MPI.
    /// Use between analysis sessions on a shared workstation so patient
    /// data from the previous TIM is never carried over.
    /// </summary>
    public void Reset()
    {
        _folders.Clear();
        _files.Clear();
        _fileStats.Clear();
        _mpi.Clear();
    }
}

public sealed class FileStats
{
    public string Format { get; }
    public int LinesValid { get; set; }
    public int LinesFiltered { get; set; }

    public FileStats(string format) => Format = format;
}

public sealed class ProcessingTotals
{
    internal int FilesProcessedRaw;
    internal int FilesSkippedRaw;
    internal long LinesValidRaw;

    public int FilesProcessed => FilesProcessedRaw;
    public int FilesSkipped => FilesSkippedRaw;
    public long LinesValid => LinesValidRaw;
    public int IppUnique { get; set; }
    public int Collisions { get; set; }
}
