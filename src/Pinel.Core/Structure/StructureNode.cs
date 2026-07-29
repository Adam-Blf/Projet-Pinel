namespace Pinel.Core.Structure;

/// <summary>
/// A node in the hospital structure tree (Territoire, Établissement,
/// Pôle, Secteur, UM). Port of the dict shape returned by
/// <c>_build_tree()</c> in <c>backend/structure.py</c>.
/// </summary>
public sealed class StructureNode
{
    public required string Code { get; init; }
    public required string Label { get; init; }
    public string? Parent { get; init; }
    public string? Level { get; init; }

    /// <summary>ARS sector type letter: G, I, D, P, Z.</summary>
    public string? SectorType { get; set; }

    public bool SectorTypeInherited { get; set; }

    public List<StructureNode> Children { get; } = new();
}

/// <summary>Summary stats produced alongside a parsed structure tree.</summary>
public sealed record StructureSummary(
    int TotalNodes,
    int Roots,
    int MaxDepth,
    Dictionary<string, int> ByLevel,
    Dictionary<string, int> BySectorType);

/// <summary>
/// Full result of parsing a structure file. Matches the JSON shape
/// emitted by the Python <c>parse_structure()</c>.
/// </summary>
public sealed record StructureResult(
    string Filename,
    string Path,
    IReadOnlyList<string> Headers,
    IReadOnlyDictionary<string, int> Mapping,
    IReadOnlyList<StructureNode> Tree,
    StructureSummary Summary);
