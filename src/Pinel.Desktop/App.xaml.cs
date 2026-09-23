using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Pinel.Core.Audit;
using Pinel.Desktop.Cli;
using Velopack;

namespace Pinel.Desktop;

/// <summary>
/// WPF application entry point. Detects <c>--headless</c> in command-line
/// args and routes to <see cref="HeadlessRunner"/> - otherwise launches
/// the standard WPF shell (MainWindow via App.xaml StartupUri).
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Point d'entree. Velopack doit passer AVANT toute fenetre : a la
    /// premiere execution apres installation ou mise a jour, il est appele
    /// avec des arguments de service, fait son travail et rend la main sans
    /// que l'utilisateur voie quoi que ce soit.
    /// </summary>
    [STAThread]
    private static void Main(string[] args)
    {
        VelopackApp.Build()
            .OnFirstRun(_ => AuditLogger.Instance.Record("/installation", "SETUP", 200, 0, "premiere execution"))
            .Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }

    // AppUserModelID - stable identifier so Windows groups windows correctly
    // and lets users pin to taskbar. Without it, single-file self-extract
    // apps look like transient bootstrappers and pinning is disabled.
    private const string AppUserModelID = "AdamBeloucif.PinelDIM";

    protected override void OnStartup(StartupEventArgs e)
    {
        // Set AppUserModelID before any window is shown - required for
        // taskbar pinning and proper shell integration.
        try
        {
            SetCurrentProcessExplicitAppUserModelID(AppUserModelID);
        }
        catch
        {
            // Shell32 unavailable - app still starts, just no pin grouping.
        }

        if (e.Args.Any(a => string.Equals(a, "--headless", StringComparison.OrdinalIgnoreCase)))
        {
            AttachToCallerConsole();
            var code = HeadlessRunner.Run(e.Args);
            Shutdown(code);
            return;
        }

        // Un seul fichier a distribuer : sans argument, Pinel ouvre sa fenetre ;
        // avec un verbe connu, il joue l'outil correspondant dans la console
        // appelante. Le DIM n'a donc qu'un executable a deployer, et la tache
        // planifiee appelle le meme.
        // Etat de la mise a jour sans ouvrir la fenetre : la direction des
        // ressources numeriques peut le verifier sur un poste a distance, ou
        // depuis une tache planifiee.
        if (e.Args.Length > 0 && string.Equals(e.Args[0], "maj", StringComparison.OrdinalIgnoreCase))
        {
            AttachToCallerConsole();
            var updates = new UpdateService();
            var state = updates.CheckAsync().GetAwaiter().GetResult();
            Console.WriteLine($"Version installee : {state.Version}");
            Console.WriteLine($"Dossier de mise a jour : {state.Source ?? "aucun"}");
            Console.WriteLine($"Version disponible : {state.Available ?? "aucune"}");
            Console.WriteLine(state.Note);
            Shutdown(state.Available is null ? 0 : 10);
            return;
        }

        if (e.Args.Length > 0 && Pinel.Cli.CommandLine.Verbs.Contains(e.Args[0]))
        {
            AttachToCallerConsole();
            Shutdown(Pinel.Cli.CommandLine.Run(e.Args));
            return;
        }
        base.OnStartup(e);
    }

    /// <summary>
    /// Rend a l'application une sortie texte utilisable. Une application WPF
    /// est compilee en sous-systeme fenetre : elle n'a pas de console, et ses
    /// ecritures partent dans le vide tant qu'on ne l'a pas rattachee a celle
    /// de l'appelant. Quand la sortie est redirigee vers un fichier ou un
    /// tube, il n'y a pas de console a rattacher, mais le descripteur existe :
    /// il suffit alors de rebrancher Console dessus.
    /// </summary>
    private static void AttachToCallerConsole()
    {
        NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS);
        Rebind(Console.OpenStandardOutput(), Console.SetOut);
        Rebind(Console.OpenStandardError(), Console.SetError);

        // Encodage de la console, et non celui par defaut d'un StreamWriter :
        // l'appelant lit la sortie avec la table de caracteres de sa console,
        // et un message en UTF-8 lui arriverait avec des accents illisibles.
        static void Rebind(Stream stream, Action<TextWriter> set)
        {
            if (stream == Stream.Null) return;
            set(new StreamWriter(stream, Console.OutputEncoding) { AutoFlush = true });
        }
    }

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(
        [MarshalAs(UnmanagedType.LPWStr)] string appID);

    private static class NativeMethods
    {
        public const int ATTACH_PARENT_PROCESS = -1;

        [DllImport("kernel32.dll")]
        public static extern bool AttachConsole(int processId);
    }
}
