namespace Pinel.Core.Checks;

/// <summary>
/// Static or streaming validator for an ATIH file. Implementations should
/// be side-effect free and deterministic so the frontend can replay
/// findings and the results can be cached per (file, mtime, validator).
/// </summary>
public interface IFileCheck
{
    /// <summary>Stable name used in UI filters and export reports.</summary>
    string Name { get; }

    /// <summary>Canonical ATIH format names this validator applies to; null = any.</summary>
    IReadOnlySet<string>? AppliesTo { get; }

    /// <summary>
    /// Runs the validator over <paramref name="filePath"/> and yields
    /// <see cref="CheckFinding"/>s as they are detected. Must not
    /// throw; file I/O errors become <see cref="CheckSeverity.Blocker"/>
    /// findings.
    /// </summary>
    IEnumerable<CheckFinding> Validate(string filePath, string formatName);
}
