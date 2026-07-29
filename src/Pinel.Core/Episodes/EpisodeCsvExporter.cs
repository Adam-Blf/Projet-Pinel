using System.Globalization;
using System.Text;
using Pinel.Core.Export;

namespace Pinel.Core.Episodes;

/// <summary>
/// Ecrit les episodes en CSV, meme convention que la moulinette : separateur
/// point-virgule, UTF-8 avec BOM.
/// </summary>
public static class EpisodeCsvExporter
{
    public static int Export(IEnumerable<Episode> episodes, string outputPath, EpisodeRules rules)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");

        using var writer = new StreamWriter(
            new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        writer.WriteLine(string.Join(RecordCsvExporter.Delimiter,
            "ID_EPISODE", "IPP", "UM", "SITE", "DATE_DEBUT", "DATE_FIN", "NB_VENUES", "DUREE_JOURS", "REGLE"));

        var rule = rules.Describe();
        int count = 0;

        foreach (var episode in episodes)
        {
            writer.WriteLine(string.Join(RecordCsvExporter.Delimiter, new[]
            {
                episode.EpisodeId,
                episode.Ipp,
                episode.Um,
                episode.Site,
                episode.Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                episode.End.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                episode.VisitCount.ToString(CultureInfo.InvariantCulture),
                episode.DurationDays.ToString(CultureInfo.InvariantCulture),
                rule,
            }.Select(RecordCsvExporter.Escape)));
            count++;
        }

        return count;
    }
}
