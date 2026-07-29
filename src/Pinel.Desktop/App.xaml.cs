using System.Runtime.InteropServices;
using System.Windows;
using Pinel.Desktop.Cli;

namespace Pinel.Desktop;

/// <summary>
/// WPF application entry point. Detects <c>--headless</c> in command-line
/// args and routes to <see cref="HeadlessRunner"/> - otherwise launches
/// the standard WPF shell (MainWindow via App.xaml StartupUri).
/// </summary>
public partial class App : Application
{
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
            NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS);
            var code = HeadlessRunner.Run(e.Args);
            Shutdown(code);
            return;
        }
        base.OnStartup(e);
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
