using System.Security.Cryptography;
using System.Text;
using Pinel.Core.Anonymization;
using Pinel.Core.Formats;
using Pinel.Core.Security;

namespace Pinel.Cli;

/// <summary>
/// Commande <c>anonymiser</c> : copie pseudonymisee d'une arborescence PMSI,
/// plus un manifeste qui ne contient que des chemins, des formats et des
/// decomptes.
/// </summary>
internal static class AnonymizeCommand
{
    public static int Run(string source, string target, string formatsDirectory)
    {
        source = Path.GetFullPath(source);
        target = Path.GetFullPath(target);
        if (!Directory.Exists(source)) throw new IOException($"Dossier source introuvable : {source}");
        if (target.StartsWith(source.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Le dossier cible ne peut pas etre a l'interieur du dossier source.");
        }

        if (CloudSyncGuard.SyncedRoot(target) is { } synced)
        {
            throw new InvalidOperationException(
                $"Le dossier cible est synchronise vers un nuage ({synced}). Des donnees de sante, meme " +
                "pseudonymisees, ne peuvent pas y etre deposees. Choisir un dossier local non synchronise.");
        }

        var specs = Specs.Load(formatsDirectory);

        var anonymizer = new PmsiAnonymizer(new Pseudonymizer(KeyStore.LoadOrCreate()), specs);
        var report = anonymizer.Run(source, target);
        var manifest = WriteManifest(report, target);

        Console.WriteLine($"{report.Written.Count} fichier(s) pseudonymise(s), {report.Excluded.Count} ecarte(s).");
        Console.WriteLine($"{report.IdentifiersSeen} identifiant(s) distinct(s) suivi(s) par le controle de fuite.");
        Console.WriteLine(report.LeaksByField.Count == 0
            ? "Controle de fuite : aucun identifiant retrouve dans un champ recopie."
            : $"Controle de fuite : {report.LeaksByField.Values.Sum()} occurrence(s) masquee(s), detail dans le manifeste.");
        Console.WriteLine($"Manifeste : {manifest}");
        return 0;
    }

    private static string WriteManifest(AnonymizationReport report, string target)
    {
        Directory.CreateDirectory(target);
        var sb = new StringBuilder();
        sb.AppendLine("# Manifeste de pseudonymisation Pinel");
        sb.AppendLine($"# {DateTime.Now:yyyy-MM-dd HH:mm}. Aucune valeur de champ dans ce fichier.");
        sb.AppendLine();
        sb.AppendLine("## Fichiers pseudonymises (chemin ; format ; lignes ; lignes a la longueur attendue)");
        foreach (var f in report.Written)
        {
            sb.AppendLine($"{f.RelativePath} ; {f.Format} ; {f.Lines} ; {f.LinesAtExpectedLength}");
        }
        sb.AppendLine();
        sb.AppendLine("## Fichiers ecartes (chemin ; motif)");
        foreach (var f in report.Excluded)
        {
            sb.AppendLine($"{f.RelativePath} ; {f.Note}");
        }
        sb.AppendLine();
        sb.AppendLine("## Controle de fuite (champ recopie ; occurrences masquees)");
        foreach (var (field, count) in report.LeaksByField.OrderByDescending(kv => kv.Value))
        {
            sb.AppendLine($"{field} ; {count}");
        }
        var path = Path.Combine(target, "MANIFESTE.txt");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        return path;
    }
}

/// <summary>
/// Cle secrete de pseudonymisation, propre au poste et a la session Windows.
/// </summary>
/// <remarks>
/// Tiree au hasard a la premiere utilisation, chiffree par DPAPI pour l'utilisateur
/// courant et rangee sous %LOCALAPPDATA%\Pinel. Elle ne quitte jamais le poste :
/// les copies produites restent reliees entre elles d'un mois sur l'autre, sans
/// qu'on puisse remonter aux valeurs d'origine ailleurs que sur ce poste.
/// </remarks>
internal static class KeyStore
{
    private static readonly string KeyPath = PinelPaths.In("anonymisation.key");

    public static byte[] LoadOrCreate()
    {
        if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("La cle est protegee par DPAPI : Windows requis.");
        if (File.Exists(KeyPath))
        {
            return ProtectedData.Unprotect(File.ReadAllBytes(KeyPath), null, DataProtectionScope.CurrentUser);
        }
        var key = RandomNumberGenerator.GetBytes(32);
        Directory.CreateDirectory(Path.GetDirectoryName(KeyPath)!);
        File.WriteAllBytes(KeyPath, ProtectedData.Protect(key, null, DataProtectionScope.CurrentUser));
        return key;
    }
}
