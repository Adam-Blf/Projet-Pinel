using Pinel.Core.Checks;
using Pinel.Core.Formats;

namespace Pinel.Cli;

/// <summary>
/// Commande <c>controler</c> : passe les controles qualite de Pinel sur un
/// dossier, tel que l'application les passerait, et n'affiche qu'un decompte
/// par code d'anomalie, avec le premier message de chaque code.
/// </summary>
/// <remarks>
/// Sert a mesurer l'application sur un lot reel pseudonymise : un code qui
/// sort par milliers sur un lot accepte par e-PMSI signale un controle faux,
/// pas un lot faux.
/// </remarks>
internal static class CheckCommand
{
    public static int Run(string folder)
    {
        var files = Directory.EnumerateFiles(folder, "*.txt", SearchOption.AllDirectories)
            .Select(p => (Path: p, Format: AtihFormatIdentifier.Identify(p)))
            .Where(f => f.Format is not null && AtihMatrix.All.ContainsKey(f.Format))
            .Select(f => (f.Path, Format: f.Format!))
            .ToList();

        Console.WriteLine($"{files.Count} fichier(s) reconnu(s) par leur nom.");
        foreach (var group in files.GroupBy(f => f.Format).OrderBy(g => g.Key))
        {
            Console.WriteLine($"   {group.Key,-14} {group.Count(),4}");
        }

        var findings = new CheckRunner().Run(files);
        Console.WriteLine();
        Console.WriteLine($"{findings.Count} anomalie(s).");
        foreach (var group in findings
                     .GroupBy(f => (f.Code, f.Severity, f.FormatName))
                     .OrderByDescending(g => g.Count()))
        {
            var first = group.First();
            Console.WriteLine($"{group.Count(),8}  {group.Key.Severity,-7} {group.Key.Code,-28} {group.Key.FormatName,-12} {first.Message}");
        }
        return 0;
    }
}
