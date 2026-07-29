using System.Text;
using Pinel.Core.Structure;

namespace Pinel.Core.Export;

/// <summary>
/// Met la structure du GHT à plat, une ligne par unité, dans un CSV que le DIM
/// peut relire et injecter dans ses outils de pilotage.
/// </summary>
/// <remarks>
/// Le cahier des charges demande des fichiers injectables dans PMSI-Pilot. Le
/// gabarit d'import attendu par PMSI-Pilot n'ayant pas été communiqué, cet
/// export fournit la structure complète à plat, avec le chemin hiérarchique et
/// le type de secteur ARS. Les colonnes seront renommées ou réordonnées dès que
/// le gabarit sera fourni, sans changement du reste de l'application.
/// </remarks>
public static class StructureCsvExporter
{
    public static int Export(StructureResult structure, string outputPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");

        using var writer = new StreamWriter(
            new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        writer.WriteLine(string.Join(RecordCsvExporter.Delimiter,
            "CODE", "LIBELLE", "NIVEAU", "PARENT", "PROFONDEUR",
            "TYPE_SECTEUR_ARS", "SECTEUR_HERITE", "CHEMIN"));

        int written = 0;
        foreach (var (node, depth, path) in Walk(structure.Tree, 0, string.Empty))
        {
            writer.WriteLine(string.Join(RecordCsvExporter.Delimiter, new[]
            {
                node.Code ?? string.Empty,
                node.Label ?? string.Empty,
                node.Level ?? string.Empty,
                node.Parent ?? string.Empty,
                depth.ToString(),
                node.SectorType ?? string.Empty,
                node.SectorTypeInherited ? "oui" : "non",
                path,
            }.Select(RecordCsvExporter.Escape)));
            written++;
        }

        return written;
    }

    private static IEnumerable<(StructureNode Node, int Depth, string Path)> Walk(
        IEnumerable<StructureNode> nodes, int depth, string parentPath)
    {
        foreach (var node in nodes)
        {
            var label = string.IsNullOrWhiteSpace(node.Code) ? node.Label ?? "" : node.Code;
            var path = parentPath.Length == 0 ? label : parentPath + " > " + label;

            yield return (node, depth, path);

            foreach (var child in Walk(node.Children, depth + 1, path))
            {
                yield return child;
            }
        }
    }
}

/// <summary>
/// Construit un nom de fichier de sortie qui n'écrase jamais un fichier
/// existant : deux lots portant le même nom de fichier source ne doivent pas
/// se remplacer silencieusement.
/// </summary>
public static class OutputPath
{
    public static string Unique(string directory, string baseName, string extension)
    {
        var candidate = Path.Combine(directory, baseName + extension);
        if (!File.Exists(candidate)) return candidate;

        for (int index = 2; index < 1000; index++)
        {
            candidate = Path.Combine(directory, $"{baseName}_{index}{extension}");
            if (!File.Exists(candidate)) return candidate;
        }

        return Path.Combine(directory, $"{baseName}_{DateTime.Now:yyyyMMdd-HHmmss}{extension}");
    }
}
