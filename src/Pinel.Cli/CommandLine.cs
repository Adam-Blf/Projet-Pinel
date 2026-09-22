using Pinel.Core.Formats;

namespace Pinel.Cli;

/// <summary>
/// Outils Pinel en ligne de commande, pour le poste DIM et le poste de
/// developpement. Aucune commande n'ecrit de donnee patient sur la console :
/// seuls des chemins, des noms de format et des decomptes y figurent.
/// </summary>
internal static class CommandLine
{
    private const string Usage =
        "Usage :\n" +
        "  pinel formats-importer <classeur.xlsx> <annee> <domaine> <dossier-sortie>\n" +
        "      Convertit un classeur officiel de formats ATIH en descriptifs Pinel.\n" +
        "  pinel roles <dossier-descriptifs>\n" +
        "      Liste les champs que la pseudonymisation transforme, format par format.\n" +
        "  pinel anonymiser <dossier-source> <dossier-cible> <dossier-descriptifs>\n" +
        "      Produit une copie pseudonymisee des fichiers PMSI reconnus.";

    public static int Run(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine(Usage);
            return 1;
        }

        try
        {
            return args[0].ToLowerInvariant() switch
            {
                "formats-importer" when args.Length == 5 => ImportFormats(args[1], int.Parse(args[2]), args[3], args[4]),
                "roles" when args.Length == 2 => RolesCommand.Run(args[1]),
                "anonymiser" when args.Length == 4 => AnonymizeCommand.Run(args[1], args[2], args[3]),
                _ => Fail(Usage),
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or InvalidOperationException)
        {
            return Fail($"[erreur] {ex.Message}");
        }
    }

    private static int ImportFormats(string workbook, int year, string domain, string output)
    {
        var layouts = AtihWorkbookImporter.Import(workbook, year, domain);
        foreach (var path in AtihWorkbookImporter.WriteAll(layouts, output))
        {
            var layout = FormatLayout.Load(path);
            Console.WriteLine($"{layout.Format,-22} {layout.Fields.Count,3} champs, partie fixe {layout.ExpectedLength,5} car.  {Path.GetFileName(path)}");
        }
        return 0;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }
}
