using System.Text.RegularExpressions;

namespace Pinel.Core.Structure;

/// <summary>
/// Parser for hospital structure files (CSV/TSV with level, code, parent,
/// label columns). Builds a tree and classifies nodes by ARS sector type.
/// Port of <c>backend/structure.py</c> (sans PDF generation).
/// </summary>
public static class StructureParser
{
    private static readonly Regex SectorCodeRe = new(
        @"^\s*(?:(\d{2,3})[-_\s]?)?([GIDPZ])[-_\s]?(\d{2,3})?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly HashSet<string> CodeCols  = new(StringComparer.OrdinalIgnoreCase)
        { "code", "id", "identifiant", "um", "code_um", "code_service" };
    private static readonly HashSet<string> ParentCols = new(StringComparer.OrdinalIgnoreCase)
        { "parent", "parent_code", "code_parent", "rattache_a", "rattachement" };
    /// <summary>
    /// Intitulés qui portent le libellé complet d'une entité. Ceux du fichier
    /// réel du service y ont été ajoutés le 23/09/2026 : sa colonne s'appelle
    /// <c>lib_um</c>, et sans cet alias l'arbre de structure sortait avec le
    /// code d'unité à la place de son nom.
    /// </summary>
    private static readonly HashSet<string> LabelCols = new(StringComparer.OrdinalIgnoreCase)
        { "label", "libelle", "libellé", "nom", "name", "designation",
          "lib_um", "libelle_um", "lib_service", "service_lib", "lib_pole", "lib_secteur" };

    /// <summary>
    /// Intitulés qui portent une forme ABRÉGÉE du libellé. Ils ne servent que
    /// si aucune colonne de libellé complet n'a été trouvée, faute de quoi
    /// l'ordre des colonnes déciderait à la place du sens : le fichier réel
    /// porte <c>lib_um_court</c> AVANT <c>lib_um</c>.
    /// </summary>
    private static readonly HashSet<string> ShortLabelCols = new(StringComparer.OrdinalIgnoreCase)
        { "lib_um_court", "libelle_court", "lib_court", "abrege" };
    private static readonly HashSet<string> LevelCols = new(StringComparer.OrdinalIgnoreCase)
        { "level", "niveau", "type" };

    private static readonly string[] FallbackOrder = { "level", "code", "parent", "label" };
    private static readonly char[] Delimiters = { ';', ',', '\t', '|' };

    /// <summary>
    /// Extracts the ARS sector letter (G/I/D/P/Z) from a sector code,
    /// or <c>null</c> if the code does not match the ARS pattern.
    /// </summary>
    public static string? DetectSectorType(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var match = SectorCodeRe.Match(code);
        return match.Success ? match.Groups[2].Value.ToUpperInvariant() : null;
    }

    public static StructureResult Parse(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Fichier introuvable : {filePath}", filePath);
        }

        var (headers, rows) = ReadRows(filePath);
        var mapping = NormalizeHeader(headers.Count > 0 ? headers : FallbackOrder.ToList());

        var tree = BuildTree(rows, mapping);
        PropagateSectorType(tree);
        var summary = Summarize(tree);

        return new StructureResult(
            Filename: Path.GetFileName(filePath),
            Path: filePath,
            Headers: headers,
            Mapping: mapping,
            Tree: tree,
            Summary: summary);
    }

    private static char DetectDelimiter(string sample)
    {
        if (string.IsNullOrEmpty(sample)) return ';';
        var firstLine = sample.Split('\n')[0];
        return Delimiters
            .Select(d => (Delim: d, Count: firstLine.Count(c => c == d)))
            .OrderByDescending(x => x.Count)
            .First()
            .Delim;
    }

    private static (List<string> Headers, List<List<string>> Rows) ReadRows(string filePath)
    {
        var text = File.ReadAllText(filePath);
        // Strip BOM if present
        if (text.Length > 0 && text[0] == '\uFEFF') text = text[1..];

        var delim = DetectDelimiter(text[..Math.Min(text.Length, 4096)]);
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var parsed = lines
            .Where(l => l.Length > 0)
            .Select(l => l.Split(delim).Select(c => c.Trim()).ToList())
            .ToList();

        if (parsed.Count == 0) return (new(), new());

        if (FirstRowIsHeader(parsed[0]))
        {
            return (parsed[0], parsed.Skip(1).ToList());
        }
        return (new(), parsed);
    }

    private static bool FirstRowIsHeader(IEnumerable<string> first)
    {
        var tokens = first.Select(c => c.ToLowerInvariant()).ToHashSet();
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in CodeCols) known.Add(s);
        foreach (var s in ParentCols) known.Add(s);
        foreach (var s in LabelCols) known.Add(s);
        foreach (var s in ShortLabelCols) known.Add(s);
        foreach (var s in LevelCols) known.Add(s);
        return tokens.Intersect(known).Any();
    }

    private static Dictionary<string, int> NormalizeHeader(IReadOnlyList<string> headers)
    {
        var mapping = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int? shortLabel = null;
        for (var i = 0; i < headers.Count; i++)
        {
            var key = headers[i]?.Trim().ToLowerInvariant() ?? string.Empty;
            if (CodeCols.Contains(key) && !mapping.ContainsKey("code")) mapping["code"] = i;
            else if (ParentCols.Contains(key) && !mapping.ContainsKey("parent")) mapping["parent"] = i;
            else if (LabelCols.Contains(key) && !mapping.ContainsKey("label")) mapping["label"] = i;
            else if (ShortLabelCols.Contains(key)) shortLabel ??= i;
            else if (LevelCols.Contains(key) && !mapping.ContainsKey("level")) mapping["level"] = i;
        }

        // Le libellé abrégé n'est retenu qu'à défaut du libellé complet.
        if (!mapping.ContainsKey("label") && shortLabel is not null)
        {
            mapping["label"] = shortLabel.Value;
        }
        for (var i = 0; i < FallbackOrder.Length && i < headers.Count; i++)
        {
            if (!mapping.ContainsKey(FallbackOrder[i]))
            {
                mapping[FallbackOrder[i]] = i;
            }
        }
        return mapping;
    }

    private static List<StructureNode> BuildTree(
        List<List<string>> rows,
        IReadOnlyDictionary<string, int> mapping)
    {
        var nodes = new Dictionary<string, StructureNode>(StringComparer.Ordinal);
        var order = new List<string>();

        mapping.TryGetValue("code", out var ci);
        mapping.TryGetValue("parent", out var pi);
        mapping.TryGetValue("label", out var li);
        mapping.TryGetValue("level", out var lvi);

        string Cell(List<string> row, int? idx)
        {
            if (!idx.HasValue || idx.Value >= row.Count) return string.Empty;
            return row[idx.Value]?.Trim() ?? string.Empty;
        }

        foreach (var row in rows)
        {
            if (row.All(c => string.IsNullOrWhiteSpace(c))) continue;

            var code = Cell(row, mapping.ContainsKey("code") ? ci : null);
            if (string.IsNullOrEmpty(code)) continue;

            var parent = Cell(row, mapping.ContainsKey("parent") ? pi : null);
            var label = Cell(row, mapping.ContainsKey("label") ? li : null);
            if (string.IsNullOrEmpty(label)) label = code;
            var level = Cell(row, mapping.ContainsKey("level") ? lvi : null);

            var sectorType = DetectSectorType(code) ?? InferSectorFromLabel(label);

            if (nodes.ContainsKey(code))
            {
                continue;
            }

            nodes[code] = new StructureNode
            {
                Code = code,
                Label = label,
                Parent = string.IsNullOrEmpty(parent) ? null : parent,
                Level = string.IsNullOrEmpty(level) ? null : level,
                SectorType = sectorType,
            };
            order.Add(code);
        }

        var roots = new List<StructureNode>();
        foreach (var code in order)
        {
            var node = nodes[code];
            if (!string.IsNullOrEmpty(node.Parent)
                && nodes.TryGetValue(node.Parent, out var p)
                && node.Parent != code)
            {
                p.Children.Add(node);
            }
            else
            {
                roots.Add(node);
            }
        }
        return roots;
    }

    private static string? InferSectorFromLabel(string label)
    {
        if (string.IsNullOrEmpty(label)) return null;
        var up = label.ToUpperInvariant();
        if (up.Contains("UMD") || up.Contains("MALADES DIFFICILES")) return "D";
        if (up.Contains("UHSA") || up.Contains("PENITENTIAIRE") || up.Contains("PÉNITENTIAIRE")) return "P";
        if (up.Contains("INFANTO") || up.Contains("PEDOPSY") || up.Contains("PÉDOPSY") || up.Contains("ENFANT") || up.Contains("ADOLESCENT")) return "I";
        if (up.Contains("INTERSECTO") || up.Contains("INTER-SECTO")) return "Z";
        return null;
    }

    private static void PropagateSectorType(IEnumerable<StructureNode> roots)
    {
        void Walk(StructureNode node, string? inherited)
        {
            if (string.IsNullOrEmpty(node.SectorType) && !string.IsNullOrEmpty(inherited))
            {
                node.SectorType = inherited;
                node.SectorTypeInherited = true;
            }
            var effective = node.SectorType ?? inherited;
            foreach (var child in node.Children) Walk(child, effective);
        }

        foreach (var r in roots) Walk(r, null);
    }

    private static StructureSummary Summarize(IEnumerable<StructureNode> roots)
    {
        var rootList = roots.ToList();
        int total = 0;
        int maxDepth = 0;
        var byLevel = new Dictionary<string, int>();
        var bySector = new Dictionary<string, int>();

        void Walk(StructureNode node, int depth)
        {
            total++;
            if (depth > maxDepth) maxDepth = depth;
            if (!string.IsNullOrEmpty(node.Level))
            {
                byLevel[node.Level] = byLevel.GetValueOrDefault(node.Level) + 1;
            }
            if (!string.IsNullOrEmpty(node.SectorType))
            {
                bySector[node.SectorType] = bySector.GetValueOrDefault(node.SectorType) + 1;
            }
            foreach (var child in node.Children) Walk(child, depth + 1);
        }

        foreach (var r in rootList) Walk(r, 1);

        return new StructureSummary(
            TotalNodes: total,
            Roots: rootList.Count,
            MaxDepth: maxDepth,
            ByLevel: byLevel,
            BySectorType: bySector);
    }
}
