using Pinel.Core.Formats;

namespace Pinel.Cli;

/// <summary>
/// Sort les lignes ATIH que transportent les exports JSON de la plateforme
/// OSPI, pour que le reste de Pinel les traite comme des fichiers ordinaires.
/// </summary>
/// <remarks>
/// Rien n'est affiché du contenu : seuls le nom du fichier, les formats
/// rencontrés et des décomptes figurent sur la console.
/// </remarks>
internal static class OspiCommand
{
    public static int Run(string sourceFolder, string targetFolder)
    {
        if (!Directory.Exists(sourceFolder))
        {
            throw new IOException($"Dossier introuvable : {sourceFolder}");
        }

        var candidates = Directory
            .GetFiles(sourceFolder, "*.json", SearchOption.AllDirectories)
            .Where(OspiJsonReader.LooksLikeOspi)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidates.Count == 0)
        {
            Console.WriteLine("Aucun export OSPI reconnu dans ce dossier.");
            return 0;
        }

        var totals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in candidates)
        {
            var extraction = OspiJsonReader.Extract(file, targetFolder);
            Console.WriteLine($"{Path.GetFileName(file)}");
            Console.WriteLine($"   {extraction.Records} enregistrements, {extraction.TotalLines} lignes");
            foreach (var (format, count) in extraction.LinesByFormat.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                Console.WriteLine($"      {format,-16} {count,8}");
                totals[format] = totals.GetValueOrDefault(format) + count;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"{candidates.Count} export(s) traite(s), {totals.Values.Sum()} lignes ecrites dans {targetFolder}");
        return 0;
    }
}
