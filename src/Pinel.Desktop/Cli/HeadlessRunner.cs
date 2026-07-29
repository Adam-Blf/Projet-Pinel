using System.IO;
using System.Text.Json;
using Pinel.Core.Formats;
using Pinel.Core.Processing;
using Pinel.Core.Checks;

namespace Pinel.Desktop.Cli;

/// <summary>
/// Command-line batch runner - exécute un scan + process + validate +
/// export sans ouvrir la fenêtre WPF. Utile pour le Planificateur Windows
/// (tâche quotidienne, cron-like). Écrit un rapport JSON sur stdout et un
/// CSV à l'emplacement demandé.
/// </summary>
/// <remarks>
/// Usage : <c>Pinel.Desktop.exe --headless --scan &lt;folder&gt; [--validate] [--export &lt;csv&gt;] [--json &lt;path&gt;]</c>
/// Codes de sortie :
/// <list type="bullet">
///   <item>0 - OK, aucun finding Blocker</item>
///   <item>1 - Paramètre invalide ou erreur I/O</item>
///   <item>2 - Au moins 1 finding Blocker - échec pré-flight DRUIDES</item>
/// </list>
/// </remarks>
public static class HeadlessRunner
{
    public static int Run(string[] args)
    {
        try
        {
            var opts = Parse(args);
            if (opts is null)
            {
                PrintUsage();
                return 1;
            }

            var processor = new AtihProcessor();
            processor.AddFolders(opts.Folders);

            Console.Error.WriteLine($"[scan] {opts.Folders.Count} dossier(s)...");
            var files = processor.Scan();
            Console.Error.WriteLine($"[scan] {files.Count} fichier(s) détecté(s).");

            Console.Error.WriteLine("[process] extraction parallèle...");
            var totals = processor.ProcessAll();
            Console.Error.WriteLine($"[process] {totals.LinesValid:N0} lignes - {totals.IppUnique:N0} IPP - {totals.Collisions} collisions.");

            IReadOnlyList<CheckFinding> findings = Array.Empty<CheckFinding>();
            Dictionary<CheckSeverity, int> summary = new();
            if (opts.Validate)
            {
                Console.Error.WriteLine("[validate] preflight DRUIDES...");
                var runner = new CheckRunner();
                findings = runner.Run(processor.Files
                    .Where(f => f.Format != "INCONNU")
                    .Select(f => (Path: f.Path, Format: f.Format)));
                summary = CheckRunner.Summarize(findings);
                Console.Error.WriteLine($"[validate] Blocker={summary.GetValueOrDefault(CheckSeverity.Blocker)} - Error={summary.GetValueOrDefault(CheckSeverity.Error)} - Warning={summary.GetValueOrDefault(CheckSeverity.Warning)} - Info={summary.GetValueOrDefault(CheckSeverity.Info)}");
            }

            int? exportedRows = null;
            if (!string.IsNullOrEmpty(opts.ExportCsvPath))
            {
                Console.Error.WriteLine($"[export] CSV → {opts.ExportCsvPath}");
                exportedRows = IdentityCsvExporter.ExportAsync(processor.Mpi, opts.ExportCsvPath).GetAwaiter().GetResult();
                Console.Error.WriteLine($"[export] {exportedRows} lignes écrites.");
            }

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
                csvPath = opts.ExportCsvPath,
            };

            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            if (!string.IsNullOrEmpty(opts.JsonReportPath))
            {
                File.WriteAllText(opts.JsonReportPath, json);
                Console.Error.WriteLine($"[report] JSON → {opts.JsonReportPath}");
            }
            else
            {
                Console.WriteLine(json);
            }

            return summary.GetValueOrDefault(CheckSeverity.Blocker) > 0 ? 2 : 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[error] {ex.Message}");
            return 1;
        }
    }

    private sealed record Options(
        IReadOnlyList<string> Folders,
        bool Validate,
        string? ExportCsvPath,
        string? JsonReportPath);

    private static Options? Parse(string[] args)
    {
        var folders = new List<string>();
        bool validate = false;
        string? export = null;
        string? json = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--headless":
                    break;
                case "--scan":
                    if (i + 1 >= args.Length) return null;
                    folders.Add(args[++i]);
                    break;
                case "--validate":
                    validate = true;
                    break;
                case "--export":
                    if (i + 1 >= args.Length) return null;
                    export = args[++i];
                    break;
                case "--json":
                    if (i + 1 >= args.Length) return null;
                    json = args[++i];
                    break;
            }
        }
        if (folders.Count == 0) return null;
        return new Options(folders, validate, export, json);
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("""
Pinel - CLI batch

  Pinel.Desktop.exe --headless --scan <folder> [--scan <folder2>...]
                          [--validate]
                          [--export <mpi.csv>]
                          [--json <report.json>]

Codes de sortie :
  0 - OK
  1 - paramètre invalide
  2 - au moins 1 Blocker détecté par preflight DRUIDES
""");
    }
}
