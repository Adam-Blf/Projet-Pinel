using System.IO;
using System.Reflection;
using Pinel.Core.Security;

namespace Pinel.Desktop;

/// <summary>
/// Locates the HTML/CSS/JS frontend bundle.
/// <para>
/// The bundle is compiled into the executable as embedded resources so the
/// application ships as a single .exe with nothing alongside it. On first
/// run of a given version, the files are written to
/// <c>%LOCALAPPDATA%\Pinel\frontend\{version}</c> and reused as-is
/// afterwards (WebView2 needs real files on disk to map a virtual host).
/// </para>
/// <para>
/// When no resource is embedded (typical of <c>dotnet run</c> during
/// development), the loose <c>frontend/</c> folder next to the binary is
/// used instead.
/// </para>
/// </summary>
public static class FrontendAssets
{
    private const string Prefix = "web/";

    private static string? _root;

    /// <summary>Directory holding index.html. Extraction happens once per process.</summary>
    public static string Root => _root ??= Resolve();

    private static string Resolve()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resources = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var loose = Path.Combine(AppContext.BaseDirectory, "web");
        if (resources.Length == 0)
        {
            return loose;
        }

        var version = assembly.GetName().Version?.ToString() ?? "dev";
        var target = Path.Combine(
            PinelPaths.DataRoot, "interface", version);

        foreach (var name in resources)
        {
            var relative = name[Prefix.Length..].Replace('\\', Path.DirectorySeparatorChar)
                                                .Replace('/', Path.DirectorySeparatorChar);
            var path = Path.Combine(target, relative);

            using var stream = assembly.GetManifestResourceStream(name);
            if (stream is null) continue;

            // Rewrite only when missing or truncated: keeps startup instant on
            // every run after the first one.
            if (File.Exists(path) && new FileInfo(path).Length == stream.Length) continue;

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var file = File.Create(path);
            stream.CopyTo(file);
        }

        return target;
    }
}
