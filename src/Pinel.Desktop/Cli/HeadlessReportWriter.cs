using System.IO;
using System.Text.Json;
using Pinel.Core.Checks;
using Pinel.Core.Processing;

namespace Pinel.Desktop.Cli;

/// <summary>
/// Compose et écrit le rapport JSON d'une exécution headless.
/// </summary>
/// <remarks>
/// Le rapport complet - dossiers, décompte par format, anomalies - ne va
/// jamais que dans le fichier dont le chemin a déjà été validé par
/// <see cref="Pinel.Core.Security.SafePath"/> avant l'appel. Seule une
/// synthèse chiffrée, sans donnée patient ni détail d'anomalie, part sur la
/// sortie erreur : la sortie standard d'une tâche planifiée finit dans un
/// journal que personne ne considère comme un fichier de données de santé.
/// </remarks>
internal static class HeadlessReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static void Write(
        AtihProcessor processor,
        ProcessingTotals totals,
        IReadOnlyList<CheckFinding> findings,
        Dictionary<CheckSeverity, int> summary,
        int? exportedRows,
        string? csvPath,
        string reportPath)
    {
        var report = new
        {
            timestamp = DateTime.UtcNow.ToString("o"),
            folders = processor.Folders,
            files = processor.Files.Count,
            filesByFormat = processor.Files.GroupBy(f => f.Format).ToDictionary(g => g.Key, g => g.Count()),
            linesValid = totals.LinesValid,
            ippUnique = totals.IppUnique,
            collisions = totals.Collisions,
            validationSummary = summary.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
            findings = findings.Take(500).ToList(),
            csvRows = exportedRows,
            csvPath,
        };

        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, JsonOptions));

        Console.Error.WriteLine(
            $"[rapport] fichiers={processor.Files.Count} - " +
            $"blockers={summary.GetValueOrDefault(CheckSeverity.Blocker)} - " +
            $"errors={summary.GetValueOrDefault(CheckSeverity.Error)} - " +
            $"warnings={summary.GetValueOrDefault(CheckSeverity.Warning)} - " +
            $"infos={summary.GetValueOrDefault(CheckSeverity.Info)} -> {reportPath}");
    }
}
