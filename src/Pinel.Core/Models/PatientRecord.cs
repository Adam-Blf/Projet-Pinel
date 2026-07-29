namespace Pinel.Core.Models;

/// <summary>
/// A (IPP, DDN) tuple extracted from a single ATIH record line.
/// </summary>
/// <param name="Ipp">Identifiant Permanent du Patient, trimmed and normalized.</param>
/// <param name="Ddn">Date De Naissance as stored (typically YYYYMMDD or DDMMYYYY).</param>
/// <param name="SourceFile">Absolute path of the file the record came from.</param>
/// <param name="LineNumber">1-indexed line number in the source file.</param>
/// <param name="Format">Canonical ATIH format name (e.g. "RPS").</param>
public sealed record PatientRecord(
    string Ipp,
    string Ddn,
    string SourceFile,
    int LineNumber,
    string Format);
