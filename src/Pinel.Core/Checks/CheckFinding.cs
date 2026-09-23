namespace Pinel.Core.Checks;

/// <summary>
/// Severity of a validation finding. Maps loosely to the DIM workflow:
/// <list type="bullet">
///   <item><see cref="Info"/> - noteworthy, not blocking</item>
///   <item><see cref="Warning"/> - likely DRUIDES rejection, must be reviewed</item>
///   <item><see cref="Error"/> - will fail DRUIDES / ATIH validation</item>
///   <item><see cref="Blocker"/> - file structurally invalid, cannot be processed</item>
/// </list>
/// </summary>
public enum CheckSeverity
{
    Info,
    Warning,
    Error,
    Blocker,
}

/// <summary>
/// One anomaly detected by a validator. Designed to be rendered in the
/// frontend grid and exported in a pre-flight report the TIM can fix
/// before sending files to DRUIDES / e-PMSI.
/// </summary>
/// <param name="Code">Stable short code ("ERR-DDN-FORMAT"). Used for filtering and analytics.</param>
/// <param name="Severity">How bad is it.</param>
/// <param name="Message">French, user-facing explanation.</param>
/// <param name="SourceFile">Absolute path of the file where the finding was raised.</param>
/// <param name="LineNumber">1-indexed line number in the source file, or 0 if file-level.</param>
/// <param name="FormatName">Canonical ATIH format name (RPS, FICHSUP-PSY, etc.), or null if unknown.</param>
/// <param name="FixHint">Optional actionable tip ("réencoder la DDN en JJMMAAAA").</param>
public sealed record CheckFinding(
    string Code,
    CheckSeverity Severity,
    string Message,
    string SourceFile,
    int LineNumber,
    string? FormatName,
    string? FixHint = null);
