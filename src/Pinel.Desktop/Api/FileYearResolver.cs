using System.IO;

namespace Pinel.Desktop.Api;

/// <summary>Résout l'année portée par un nom de fichier, partagée entre les points de terminaison qui choisissent un descriptif ATIH selon le millésime.</summary>
internal static class FileYearResolver
{
    /// <summary>Année portée par le nom de fichier, pour choisir le bon descriptif.</summary>
    internal static int? Resolve(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        for (int i = 0; i + 4 <= name.Length; i++)
        {
            if (int.TryParse(name.AsSpan(i, 4), out var year) && year is >= 2000 and <= 2100)
            {
                return year;
            }
        }
        return null;
    }
}
