using Pinel.Core.Models;

namespace Pinel.Core.Identity;

/// <summary>
/// Master Patient Index built from <see cref="PatientRecord"/>s across
/// all ATIH files. Detects collisions (one IPP mapped to multiple DDN).
/// Port of MPI logic from <c>DataProcessor</c> in Python.
/// </summary>
public sealed class MasterPatientIndex
{
    private readonly Dictionary<string, MpiEntry> _index = new(StringComparer.Ordinal);

    /// <summary>
    /// Appends one observation to the index. If the IPP is new, a fresh
    /// <see cref="MpiEntry"/> is created. Otherwise the DDN is added to
    /// the existing entry's history and the pivot is re-elected.
    /// </summary>
    public void Add(PatientRecord record)
    {
        if (!_index.TryGetValue(record.Ipp, out var entry))
        {
            entry = new MpiEntry(record.Ipp);
            _index[record.Ipp] = entry;
        }
        entry.AddObservation(record.Ddn, record.SourceFile);
    }

    /// <summary>Returns the entry for <paramref name="ipp"/> or <c>null</c>.</summary>
    public MpiEntry? Get(string ipp) =>
        _index.TryGetValue(ipp, out var e) ? e : null;

    /// <summary>All entries currently stored in the index.</summary>
    public IReadOnlyCollection<MpiEntry> All => _index.Values;

    /// <summary>Entries where the same IPP maps to two or more distinct DDN values.</summary>
    public IEnumerable<MpiEntry> Collisions =>
        _index.Values.Where(e => e.DistinctDdnCount > 1);

    /// <summary>Number of unique IPP entries.</summary>
    public int Count => _index.Count;

    /// <summary>Empties the index. Required between sessions for GDPR compliance.</summary>
    public void Clear() => _index.Clear();
}

/// <summary>
/// One patient's history in the MPI. Stores every observed DDN and the
/// source files where it appeared. <see cref="Pivot"/> is the auto-resolved
/// canonical DDN (most frequent - ties broken by recency in calling code).
/// </summary>
public sealed class MpiEntry
{
    public string Ipp { get; }
    public string? Pivot { get; set; }
    public Dictionary<string, List<string>> History { get; } = new(StringComparer.Ordinal);

    public MpiEntry(string ipp) => Ipp = ipp;

    public int DistinctDdnCount => History.Count;

    internal void AddObservation(string ddn, string sourceFile)
    {
        if (!History.TryGetValue(ddn, out var sources))
        {
            sources = new List<string>();
            History[ddn] = sources;
        }
        sources.Add(sourceFile);

        // Auto-resolve pivot: most frequent DDN wins.
        if (Pivot is null || History[ddn].Count > (History.TryGetValue(Pivot, out var p) ? p.Count : 0))
        {
            Pivot = ddn;
        }
    }
}
