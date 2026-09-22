using Pinel.Core.Anonymization;
using Pinel.Core.Formats;

namespace Pinel.Cli;

/// <summary>
/// Commande <c>roles</c> : affiche, pour chaque descriptif d'un dossier, les
/// champs que la pseudonymisation transforme. A relire a chaque nouveau
/// millesime ATIH : un identifiant rebaptise que le classement ne reconnait
/// plus apparaitrait ici par son absence.
/// </summary>
internal static class RolesCommand
{
    public static int Run(string directory)
    {
        foreach (var path in Directory.GetFiles(directory, "*.format.csv").OrderBy(p => p))
        {
            var layout = FormatLayout.Load(path);
            Console.WriteLine($"== {layout.Format}{(layout.HasRepeatZone ? " (zone repetee)" : "")}");
            foreach (var field in layout.Fields)
            {
                var role = FieldClassifier.Classify(field);
                if (role != FieldRole.Keep) Console.WriteLine($"   {role,-12} {field.Start,4}+{field.Length,-3} {field.Label}");
            }
        }
        return 0;
    }
}
