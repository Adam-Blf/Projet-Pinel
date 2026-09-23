namespace Pinel.Core.Security;

/// <summary>
/// Repere un dossier synchronise vers un nuage grand public (Google Drive,
/// OneDrive, Dropbox, iCloud).
/// </summary>
/// <remarks>
/// <para>
/// Des donnees de sante, meme pseudonymisees, ne peuvent etre hebergees que
/// chez un hebergeur certifie HDS (article L1111-8 du code de la sante
/// publique). Un dossier synchronise envoie tout ce qu'on y depose, sans que
/// son nom l'annonce : le dossier Documents d'un poste peut remonter vers
/// Google Drive en entier.
/// </para>
/// <para>
/// Le reperage suit les marqueurs que les clients de synchronisation posent a
/// la racine de ce qu'ils synchronisent, en remontant l'arborescence, plus les
/// variables d'environnement de OneDrive.
/// </para>
/// </remarks>
public static class CloudSyncGuard
{
    private static readonly string[] Markers =
    {
        ".tmp.driveupload",   // Google Drive pour ordinateur
        ".dropbox",           // Dropbox
        ".dropbox.cache",
        "desktop.ini.icloud", // iCloud
    };

    private static readonly string[] PathHints =
    {
        "\\GOOGLE DRIVE\\", "\\MY DRIVE\\", "\\MON DRIVE\\", "\\ONEDRIVE", "\\DROPBOX\\", "\\ICLOUDDRIVE\\",
    };

    /// <summary>
    /// Rend le dossier synchronise qui contient <paramref name="path"/>, ou null
    /// si aucun marqueur n'est trouve.
    /// </summary>
    public static string? SyncedRoot(string path)
    {
        var full = Path.GetFullPath(path).TrimEnd('\\') + "\\";
        var upper = full.ToUpperInvariant();

        foreach (var variable in new[] { "OneDrive", "OneDriveCommercial", "OneDriveConsumer" })
        {
            var root = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrEmpty(root) && upper.StartsWith(root.TrimEnd('\\').ToUpperInvariant() + "\\", StringComparison.Ordinal))
            {
                return root;
            }
        }
        foreach (var hint in PathHints)
        {
            if (upper.Contains(hint, StringComparison.Ordinal)) return full;
        }

        for (var dir = new DirectoryInfo(full); dir is not null; dir = dir.Parent)
        {
            foreach (var marker in Markers)
            {
                var candidate = Path.Combine(dir.FullName, marker);
                if (Directory.Exists(candidate) || File.Exists(candidate)) return dir.FullName;
            }
        }
        return null;
    }
}
