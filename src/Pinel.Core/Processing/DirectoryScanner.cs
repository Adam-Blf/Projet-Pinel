using Pinel.Core.Formats;

namespace Pinel.Core.Processing;

/// <summary>
/// Recursively scans folders for ATIH .txt and .csv files and classifies
/// each by format via <see cref="AtihFormatIdentifier"/>.
/// Port of <c>DataProcessor.scan_directory</c> + <c>scan_multiple_directories</c>.
/// </summary>
public static class DirectoryScanner
{
    public static IReadOnlyList<ScannedFile> Scan(IEnumerable<string> folders)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<ScannedFile>();

        foreach (var folder in folders)
        {
            if (!Directory.Exists(folder)) continue;
            foreach (var file in EnumerateCandidates(folder))
            {
                if (!seen.Add(file)) continue;
                result.Add(ToScannedFile(file));
            }
        }

        return result;
    }

    private static IEnumerable<string> EnumerateCandidates(string folder)
    {
        var extensions = new[] { "*.txt", "*.csv" };
        foreach (var pattern in extensions)
        {
            string[] files;
            try
            {
                files = Directory.GetFiles(folder, pattern, SearchOption.AllDirectories);
            }
            catch (UnauthorizedAccessException)
            {
                files = Array.Empty<string>();
            }
            foreach (var f in files) yield return f;
        }
    }

    private static ScannedFile ToScannedFile(string path)
    {
        var fmt = AtihFormatIdentifier.Identify(path) ?? "INCONNU";
        long size = 0;
        try { size = new FileInfo(path).Length; } catch (IOException) { /* best effort */ }

        return new ScannedFile(
            Path: path,
            Name: System.IO.Path.GetFileName(path),
            Format: fmt,
            SizeKb: Math.Round(size / 1024d, 1),
            Dir: System.IO.Path.GetDirectoryName(path) ?? string.Empty);
    }
}

public sealed record ScannedFile(
    string Path,
    string Name,
    string Format,
    double SizeKb,
    string Dir);
