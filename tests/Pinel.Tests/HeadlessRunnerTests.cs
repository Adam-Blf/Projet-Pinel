using System.Diagnostics;

namespace Pinel.Tests;

/// <summary>
/// Preuve boîte noire du CLI headless (<c>Pinel.exe --headless</c>).
/// </summary>
/// <remarks>
/// <para>
/// <c>Pinel.Tests</c> ne référence pas l'assembly <c>Pinel.Desktop</c> (WPF,
/// <c>net8.0-windows</c>) : lui ajouter une référence de projet dépasserait
/// le fichier que ces tests ont le droit de modifier. Ces tests lancent donc
/// l'exécutable déjà construit par la même solution, en sous-processus,
/// plutôt que d'appeler <c>HeadlessRunner.Run</c> en mémoire. Cela suppose
/// que <c>Pinel.Desktop</c> a été construit dans la MÊME configuration que
/// <c>Pinel.Tests</c> - c'est le cas dès que la solution entière a été
/// construite (<c>dotnet build Pinel.sln</c>), mais PAS garanti par
/// <c>dotnet test Pinel.sln</c> seul, qui ne construit que le graphe de
/// dépendances de chaque projet de test. <see cref="LocateExecutable"/>
/// compare donc la date de l'exécutable à celle des sources du CLI et échoue
/// avec un message explicite plutôt que de lire silencieusement un binaire
/// périmé.
/// </para>
/// <para>
/// L'isolement des dossiers autorisés passe par la variable d'environnement
/// <c>PINEL_WORKSPACE</c>, le mécanisme de déploiement par GPO déjà prévu
/// par <see cref="Pinel.Core.Security.SafePath"/> : chaque test pose son
/// propre dossier temporaire sans toucher au fichier de réglages réel de la
/// machine.
/// </para>
/// </remarks>
public sealed class HeadlessRunnerTests
{
    private static readonly string RepositoryRoot = LocateRepositoryRoot();
    private static readonly string ExecutablePath = LocateExecutable();
    private const int TimeoutMs = 30_000;

    [Fact]
    public void Refuse_le_scan_d_un_dossier_hors_des_racines_autorisees()
    {
        using var allowed = new TempFolder();
        using var outside = new TempFolder();
        outside.WriteFile("RPS_FICHIER.txt", "contenu factice");

        var result = RunHeadless(
            environmentWorkspace: allowed.Path,
            args: new[] { "--headless", "--scan", outside.Path });

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("autorisés", result.StdErr);
        Assert.Contains(allowed.Path, result.StdErr);
        // Refusé avant tout scan : rien n'a été ajouté au dossier refusé.
        Assert.Single(Directory.GetFiles(outside.Path));
    }

    [Fact]
    public void Refuse_l_export_csv_hors_des_racines_autorisees()
    {
        using var allowed = new TempFolder();
        using var outside = new TempFolder();
        allowed.WriteFile("RPS_FICHIER.txt", "contenu factice");
        var csvOutside = Path.Combine(outside.Path, "mpi.csv");

        var result = RunHeadless(
            environmentWorkspace: allowed.Path,
            args: new[] { "--headless", "--scan", allowed.Path, "--export", csvOutside });

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("autorisés", result.StdErr);
        Assert.False(File.Exists(csvOutside));
    }

    [Fact]
    public void Refuse_le_rapport_json_hors_des_racines_autorisees()
    {
        using var allowed = new TempFolder();
        using var outside = new TempFolder();
        allowed.WriteFile("RPS_FICHIER.txt", "contenu factice");
        var jsonOutside = Path.Combine(outside.Path, "rapport.json");

        var result = RunHeadless(
            environmentWorkspace: allowed.Path,
            args: new[] { "--headless", "--scan", allowed.Path, "--json", jsonOutside });

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("autorisés", result.StdErr);
        Assert.False(File.Exists(jsonOutside));
    }

    [Fact]
    public void Accepte_un_scan_et_un_export_sous_une_racine_autorisee()
    {
        using var allowed = new TempFolder();
        allowed.WriteFile("RPS_FICHIER.txt", "contenu factice");
        var csvInside = Path.Combine(allowed.Path, "mpi.csv");
        var jsonInside = Path.Combine(allowed.Path, "rapport.json");

        var result = RunHeadless(
            environmentWorkspace: allowed.Path,
            args: new[]
            {
                "--headless", "--scan", allowed.Path,
                "--export", csvInside,
                "--json", jsonInside,
            });

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(csvInside));
        Assert.True(File.Exists(jsonInside));
        Assert.Contains("findings", File.ReadAllText(jsonInside));
    }

    [Fact]
    public void N_imprime_plus_le_rapport_complet_sur_la_sortie_standard()
    {
        using var allowed = new TempFolder();
        allowed.WriteFile("RPS_FICHIER.txt", "contenu factice");
        var defaultWorkspace = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pinel", "travail");
        Directory.CreateDirectory(defaultWorkspace);
        var before = Directory.GetFiles(defaultWorkspace, "rapport-pinel-*.json").ToHashSet();

        var result = RunHeadless(
            environmentWorkspace: allowed.Path,
            args: new[] { "--headless", "--scan", allowed.Path });

        try
        {
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(string.Empty, result.StdOut.Trim());
            Assert.DoesNotContain("findings", result.StdOut);

            var after = Directory.GetFiles(defaultWorkspace, "rapport-pinel-*.json").ToHashSet();
            var created = after.Except(before).ToList();
            Assert.Single(created);
            Assert.Contains("findings", File.ReadAllText(created[0]));
        }
        finally
        {
            foreach (var file in Directory.GetFiles(defaultWorkspace, "rapport-pinel-*.json").Except(before))
            {
                File.Delete(file);
            }
        }
    }

    private static (int ExitCode, string StdOut, string StdErr) RunHeadless(
        string environmentWorkspace, string[] args)
    {
        var psi = new ProcessStartInfo(ExecutablePath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        psi.Environment["PINEL_WORKSPACE"] = environmentWorkspace;

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Impossible de démarrer " + ExecutablePath);

        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();
        var exited = process.WaitForExit(TimeoutMs);

        if (!exited)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(
                $"Pinel.exe --headless n'a pas rendu la main en {TimeoutMs} ms : régression de blocage " +
                "(le CLI headless ne pompe jamais le Dispatcher WPF, un Task bloqué dessus ne revient jamais).");
        }

        return (process.ExitCode, stdOutTask.GetAwaiter().GetResult(), stdErrTask.GetAwaiter().GetResult());
    }

    private static string LocateRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Pinel.sln")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName
            ?? throw new InvalidOperationException("Pinel.sln introuvable depuis " + AppContext.BaseDirectory);
    }

    private static string LocateExecutable()
    {
#if DEBUG
        string[] configs = { "Debug", "Release" };
#else
        string[] configs = { "Release", "Debug" };
#endif
        var candidates = configs
            .Select(config => Path.Combine(
                RepositoryRoot, "src", "Pinel.Desktop", "bin", config, "net8.0-windows", "win-x64", "Pinel.exe"))
            .ToList();

        var exe = candidates.FirstOrDefault(File.Exists)
            ?? throw new InvalidOperationException(
                "Pinel.exe introuvable (" + string.Join(", ", candidates) + "). " +
                "Lancer dotnet build Pinel.sln avant les tests.");

        var sourceDir = Path.Combine(RepositoryRoot, "src", "Pinel.Desktop", "Cli");
        var newestSource = Directory.GetFiles(sourceDir, "*.cs")
            .Select(File.GetLastWriteTimeUtc)
            .DefaultIfEmpty(DateTime.MinValue)
            .Max();

        if (newestSource > File.GetLastWriteTimeUtc(exe))
        {
            throw new InvalidOperationException(
                $"{exe} est plus ancien que src/Pinel.Desktop/Cli : binaire périmé. " +
                "Lancer dotnet build Pinel.sln avant les tests.");
        }

        return exe;
    }

    /// <summary>Dossier temporaire jetable, supprimé même si le test échoue.</summary>
    private sealed class TempFolder : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("pinel-headless-tests-").FullName;

        public void WriteFile(string name, string content) =>
            File.WriteAllText(System.IO.Path.Combine(Path, name), content);

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); }
            catch (IOException) { /* fichier verrouillé transitoirement : tant pis, dossier temp */ }
        }
    }
}
