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
/// <param name="CarriesPatientIdentifiers">
/// False when the official ATIH descriptor declares no usable patient
/// identifier for this format. Two cases, both established on the 2026
/// descriptors: files that simply carry none (FICHCOMP transports, the
/// aggregated FICHSUP), and anonymised outputs of PIVOINE where the IPP has
/// been replaced by an irreversible hash (RPSA, R3A). Reading a hash and
/// filing it as an IPP would rebuild, in the patient index, the very link
/// that MAGIC and PIVOINE exist to sever. Parsers must skip extraction
/// entirely rather than store whatever bytes sit at the nominal offsets.
/// Last parameter and defaulted to true, so every existing positional
/// construction keeps compiling and behaving as before.
/// </param>
public sealed record AtihFormat(
    string Name,
    int Length,
    int IppStart,
    int IppEnd,
    int DdnStart,
    int DdnEnd,
    string Description,
    string Field,
    int Since,
    bool CarriesPatientIdentifiers = true)
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
