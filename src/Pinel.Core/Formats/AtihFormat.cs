namespace Pinel.Core.Formats;

/// <summary>
/// Describes a fixed-width ATIH file format used by the French PMSI system.
/// IPP and DDN positions are zero-indexed half-open ranges [start, end).
/// Port of <c>ATIH_MATRIX</c> entries from <c>backend/data_processor.py</c>.
/// </summary>
/// <param name="Name">Canonical format code (e.g. "RPS", "VID-HOSP").</param>
/// <param name="Length">Canonical record length in characters.</param>
/// <param name="IppStart">IPP field start (inclusive, 0-indexed).</param>
/// <param name="IppEnd">IPP field end (exclusive).</param>
/// <param name="DdnStart">DDN field start (inclusive, 0-indexed).</param>
/// <param name="DdnEnd">DDN field end (exclusive).</param>
/// <param name="Description">Human-readable French description.</param>
/// <param name="Field">PMSI field family (PSY, MCO, SSR, HAD, TRANSVERSAL).</param>
/// <param name="Since">Year the format was introduced.</param>
public sealed record AtihFormat(
    string Name,
    int Length,
    int IppStart,
    int IppEnd,
    int DdnStart,
    int DdnEnd,
    string Description,
    string Field,
    int Since)
{
    public int IppLength => IppEnd - IppStart;
    public int DdnLength => DdnEnd - DdnStart;
}

/// <summary>
/// Alternative length variant for legacy (typically 2021) files that
/// did not yet match the current canonical length.
/// </summary>
public sealed record AtihFormatVariant(
    int Length,
    int IppStart,
    int IppEnd,
    int DdnStart,
    int DdnEnd,
    string Label);
