using System.IO;

namespace Pinel.Core.Security;

/// <summary>
/// Emplacement unique des donnees de travail de Pinel sur le poste.
/// </summary>
/// <remarks>
/// <para>
/// <c>%LOCALAPPDATA%\Pinel-DIM</c>, et surtout pas <c>%LOCALAPPDATA%\Pinel</c> :
/// depuis que l'application s'installe et se met a jour toute seule,
/// l'installateur est proprietaire du dossier qui porte le nom du produit. Il y
/// range les versions, y fait le menage a chaque mise a jour et l'efface a la
/// desinstallation. Les reglages, les descriptifs de format, le journal
/// d'audit, les regles apprises, le modele et la cle de pseudonymisation y
/// auraient disparu avec lui.
/// </para>
/// <para>
/// Dossier local et non itinerant : le modele pese quelques megaoctets et la
/// cle ne doit pas se promener sur le reseau a chaque ouverture de session.
/// </para>
/// </remarks>
public static class PinelPaths
{
    /// <summary>Racine des donnees de travail, creee a la demande.</summary>
    public static string DataRoot { get; } = Resolve();

    private static string Resolve()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var root = Path.Combine(local, "Pinel-DIM");
        Directory.CreateDirectory(root);
        Recover(Path.Combine(local, "Pinel"), root);
        return root;
    }

    /// <summary>
    /// Reprend les donnees d'une installation anterieure, quand elles vivaient
    /// encore dans le dossier que l'installateur s'est appropprie depuis. Les
    /// dossiers de l'installateur sont laisses ou ils sont ; seules les pieces
    /// connues sont deplacees, et jamais par-dessus un fichier deja present.
    /// </summary>
    private static void Recover(string previous, string root)
    {
        string[] known =
        {
            "settings.json", "anonymisation.key", "maj.json", "regles-apprises.json",
            "formats", "travail", "modele",
        };
        if (!Directory.Exists(previous) || string.Equals(previous, root, StringComparison.OrdinalIgnoreCase)) return;

        foreach (var name in known)
        {
            var from = Path.Combine(previous, name);
            var to = Path.Combine(root, name);
            try
            {
                if (File.Exists(from) && !File.Exists(to)) File.Move(from, to);
                else if (Directory.Exists(from) && !Directory.Exists(to)) Directory.Move(from, to);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Piece verrouillee : elle restera a l'ancienne adresse, et
                // Pinel repartira d'un reglage par defaut plutot que d'echouer.
            }
        }
    }

    /// <summary>Chemin sous la racine, dossiers parents crees.</summary>
    public static string In(params string[] parts)
    {
        var path = Path.Combine(new[] { DataRoot }.Concat(parts).ToArray());
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? DataRoot);
        return path;
    }
}
