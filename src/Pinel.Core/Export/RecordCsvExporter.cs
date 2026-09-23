using System.Text;
using Pinel.Core.Formats;

namespace Pinel.Core.Export;

/// <summary>
/// Resultat d'un export : ou, combien de lignes, et sur quel descriptif.
/// </summary>
public sealed record ExportResult(
    string OutputPath,
    string Format,
    int Lines,
    int Columns,
    string LayoutSource,
    bool RawFallback);

/// <summary>
/// La moulinette a format du cahier des charges : un fichier ATIH a largeur
/// fixe entre, un CSV exploitable sort.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>separateur point-virgule, comme demande par le DIM ;</item>
///   <item>UTF-8 avec BOM, pour qu'Excel ouvre le fichier sans assistant
///     d'importation et sans casser les accents ;</item>
///   <item>lecture en ISO-8859-1, encodage des fichiers ATIH ;</item>
///   <item>une colonne par champ du descriptif, plus les colonnes de
///     tracabilite FICHIER_SOURCE et NUM_LIGNE ;</item>
///   <item>si aucun descriptif n'est disponible pour le format, la ligne
///     brute est exportee telle quelle dans une colonne LIGNE_BRUTE : mieux
///     vaut un CSV honnete qu'un decoupage invente.</item>
/// </list>
/// </remarks>
public static class RecordCsvExporter
{
    public const char Delimiter = ';';



    /// <summary>
    /// Exporte un fichier ATIH vers <paramref name="outputPath"/>.
    /// </summary>
    /// <param name="sourcePath">Fichier ATIH a lire.</param>
    /// <param name="format">Nom du format reconnu.</param>
    /// <param name="outputPath">CSV a ecrire.</param>
    /// <param name="layout">Descriptif a appliquer, null pour l'export brut.</param>
    public static ExportResult Export(
        string sourcePath,
        string format,
        string outputPath,
        FormatLayout? layout)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");

        using var reader = new StreamReader(sourcePath, PmsiEncoding.Latin1);
        using var writer = new StreamWriter(
            new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        var fileName = Path.GetFileName(sourcePath);
        var raw = layout is null || layout.Fields.Count == 0;

        var header = new List<string> { "FICHIER_SOURCE", "NUM_LIGNE", "FORMAT" };
        header.AddRange(raw ? new[] { "LIGNE_BRUTE" } : layout!.Fields.Select(f => f.Name));
        writer.WriteLine(string.Join(Delimiter, header.Select(Escape)));

        int lineNumber = 0;
        int exported = 0;

        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            if (line.Trim().Length == 0) continue;

            var values = new List<string> { fileName, lineNumber.ToString(), format };
            values.AddRange(raw ? new[] { line.TrimEnd() } : layout!.Split(line));

            writer.WriteLine(string.Join(Delimiter, values.Select(Escape)));
            exported++;
        }

        return new ExportResult(
            OutputPath: outputPath,
            Format: format,
            Lines: exported,
            Columns: header.Count,
            LayoutSource: layout?.Source ?? string.Empty,
            RawFallback: raw);
    }

    /// <summary>
    /// Exporte un lot de fichiers dans <paramref name="outputDirectory"/>,
    /// un CSV par fichier source, nom d'origine suffixe en .csv.
    /// </summary>
    public static IReadOnlyList<ExportResult> ExportBatch(
        IEnumerable<(string Path, string Format)> files,
        string outputDirectory,
        LayoutRegistry registry,
        Func<string, int?>? yearResolver = null)
    {
        Directory.CreateDirectory(outputDirectory);
        var results = new List<ExportResult>();

        foreach (var (path, format) in files)
        {
            var year = yearResolver?.Invoke(path);
            var layout = registry.Resolve(format, year);
            var output = Path.Combine(
                outputDirectory,
                Path.GetFileNameWithoutExtension(path) + ".csv");

            results.Add(Export(path, format, output, layout));
        }

        return results;
    }

    /// <summary>
    /// Neutralise le point-virgule, les guillemets, les sauts de ligne, et les
    /// valeurs qu'Excel interpreterait comme une formule.
    /// </summary>
    internal static string Escape(string field)
    {
        if (field.Length > 0 && "=+-@\t\r".IndexOf(field[0]) >= 0)
        {
            field = "'" + field;
        }

        if (field.IndexOf(Delimiter) >= 0 || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        }

        return field;
    }
}
