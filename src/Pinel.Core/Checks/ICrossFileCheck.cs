namespace Pinel.Core.Checks;

/// <summary>
/// Validator that correlates records across MULTIPLE files rather than a
/// single one. Typical use cases: chaînage VID-HOSP/RPS, FICHCOMP →
/// séjour orphan check, FicUM → UM orphan check. Kept distinct from
/// <see cref="IFileCheck"/> so the runner can dispatch each appropriately.
/// </summary>
public interface ICrossFileCheck
{
    string Name { get; }

    /// <summary>
    /// Runs across <paramref name="files"/>. Yields findings tied to a
    /// specific source file when possible, or the batch as a whole
    /// (<c>LineNumber == 0</c>) when the anomaly is aggregate.
    /// </summary>
    IEnumerable<CheckFinding> Validate(IReadOnlyList<(string Path, string Format)> files);
}
