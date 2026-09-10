using System.IO;
using Pinel.Core.Checks;
using Pinel.Core.Processing;
using Pinel.Core.Security;

namespace Pinel.Desktop.Cli;

/// <summary>
/// Command-line batch runner - exécute un scan + process + validate +
/// export sans ouvrir la fenêtre WPF. Utile pour le Planificateur Windows
/// (tâche quotidienne, cron-like).
/// </summary>
/// <remarks>
/// <para>
/// Usage : <c>Pinel.Desktop.exe --headless --scan &lt;folder&gt; [--validate] [--export &lt;csv&gt;] [--json &lt;path&gt;]</c>
/// </para>
/// <para>
/// Les dossiers scannés et les deux chemins de sortie sont validés par
/// <see cref="SafePath"/> contre les dossiers de travail autorisés, chargés
/// depuis <see cref="WorkspaceSettings"/> avant tout traitement. Le rapport
/// JSON n'est jamais imprimé sur la sortie standard : elle finit dans le
/// journal du Planificateur de tâches, que personne ne surveille comme un
/// fichier de données de santé.
/// </para>
/// Codes de sortie :
/// <list type="bullet">
///   <item>0 - OK, aucun finding Blocker</item>
///   <item>1 - Paramètre invalide, chemin refusé, ou erreur I/O</item>
///   <item>2 - Au moins 1 finding Blocker - échec pré-flight DRUIDES</item>
/// </list>
/// </remarks>
public static class HeadlessRunner
{
    /// <param name="args">Arguments de la ligne de commande.</param>
    /// <param name="settings">
    /// Réglages à utiliser à la place de <see cref="WorkspaceSettings.Load()"/>.
    /// Réservé aux tests : le point d'entrée réel (<c>App.xaml.cs</c>)
    /// n'en fournit jamais, pour toujours lire les dossiers de travail que
    /// l'utilisateur a réellement autorisés dans l'application.
    /// </param>
    public static int Run(string[] args, WorkspaceSettings? settings = null)
    {
        try
        {
            var opts = Parse(args);
            if (opts is null)
            {
                PrintUsage();
                return 1;
            }

            // Chargées avant tout accès fichier : sans cet appel, SafePath ne
            // connaît que l'espace de travail par défaut et toute vérification
            // en aval serait sans effet sur les dossiers réellement autorisés.
            settings ??= WorkspaceSettings.Load();
            SafePath.Reload(settings);

            var scanFolders = new List<string>();
            foreach (var folder in opts.Folders)
            {
                var resolved = SafePath.TryRequire(folder);
                if (resolved is null || !Directory.Exists(resolved))
                {
                    Console.Error.WriteLine(RefusalMessage("dossier à scanner", folder));
                    return 1;
                }
                scanFolders.Add(resolved);
            }

            string? exportPath = null;
            if (!string.IsNullOrEmpty(opts.ExportCsvPath))
            {
                exportPath = SafePath.TryRequire(opts.ExportCsvPath);
                if (exportPath is null)
                {
                    Console.Error.WriteLine(RefusalMessage("export CSV", opts.ExportCsvPath));
                    return 1;
                }
            }

            var reportPath = ResolveReportPath(opts.JsonReportPath, settings);
            if (reportPath is null)
            {
                Console.Error.WriteLine(RefusalMessage("rapport JSON", opts.JsonReportPath ?? "(chemin par défaut)"));
                return 1;
            }

            var processor = new AtihProcessor();
            processor.AddFolders(scanFolders);

            Console.Error.WriteLine($"[scan] {scanFolders.Count} dossier(s)...");
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
            if (exportPath is not null)
            {
                Console.Error.WriteLine($"[export] CSV -> {exportPath}");
                // Task.Run détache l'appel du contexte de synchronisation WPF :
                // le CLI headless ne pompe jamais le Dispatcher (il appelle
                // Shutdown() juste après), donc un ConfigureAwait implicite
                // dans l'exporteur bloquerait cette continuation pour
                // toujours. C'est le CLI qui doit s'en protéger, pas
                // l'exporteur, partagé avec le bridge HTTP qui n'a pas ce
                // problème.
                exportedRows = Task.Run(() => IdentityCsvExporter.ExportAsync(processor.Mpi, exportPath)).GetAwaiter().GetResult();
                Console.Error.WriteLine($"[export] {exportedRows} lignes écrites.");
            }

            HeadlessReportWriter.Write(processor, totals, findings, summary, exportedRows, exportPath, reportPath);

            return summary.GetValueOrDefault(CheckSeverity.Blocker) > 0 ? 2 : 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[error] {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Chemin du rapport JSON, toujours confiné par <see cref="SafePath"/> :
    /// celui demandé par l'utilisateur s'il est autorisé, sinon un nom généré
    /// sous le dossier de sortie courant. Jamais un repli sur la sortie
    /// standard.
    /// </summary>
    private static string? ResolveReportPath(string? requested, WorkspaceSettings settings)
    {
        if (!string.IsNullOrEmpty(requested))
        {
            return SafePath.TryRequire(requested);
        }

        var outputFolder = settings.ResolveOutputFolder();
        Directory.CreateDirectory(outputFolder);
        var generated = Path.Combine(outputFolder, $"rapport-pinel-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
        return SafePath.TryRequire(generated);
    }

    /// <summary>
    /// Message de refus nommant une cible atteignable : les dossiers
    /// réellement autorisés et le geste pour y remédier, plutôt qu'un simple
    /// constat que l'utilisateur ne peut pas satisfaire.
    /// </summary>
    private static string RefusalMessage(string context, string candidate) =>
        $"[refus] Chemin refusé ({context}) : '{candidate}' est hors des dossiers de travail autorisés. " +
        $"Dossiers autorisés actuellement : {string.Join(", ", SafePath.Roots)}. " +
        "Ajoutez ce dossier via l'application (écran Dossiers de travail) avant de le scanner en CLI, " +
        "ou choisissez un chemin sous l'un des dossiers ci-dessus.";

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

Les dossiers scannés et les chemins de sortie doivent être sous l'un des
dossiers de travail autorisés (écran Dossiers de travail de l'application).
Sans --json, le rapport est écrit dans le dossier de sortie configuré, sous
un nom généré automatiquement ; il n'est jamais imprimé sur la sortie
standard.

Codes de sortie :
  0 - OK
  1 - paramètre invalide ou chemin refusé
  2 - au moins 1 Blocker détecté par preflight DRUIDES
""");
    }
}
