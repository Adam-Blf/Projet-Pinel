using Pinel.Core.Formats;

namespace Pinel.Cli;

/// <summary>
/// Outils Pinel en ligne de commande, pour le poste DIM et le poste de
/// developpement. Aucune commande n'ecrit de donnee patient sur la console :
/// seuls des chemins, des noms de format et des decomptes y figurent.
/// </summary>
/// <remarks>
/// Ces commandes sont aussi servies par Pinel.exe, l'application de bureau :
/// un seul fichier a distribuer, qui ouvre la fenetre sans argument et joue
/// l'outil demande avec un verbe connu. Voir <see cref="Verbs"/>.
/// </summary>
public static class CommandLine
{
    private const string Usage =
        "Usage :\n" +
        "  pinel formats-importer <classeur.xlsx> <annee> <domaine> <dossier-sortie>\n" +
        "      Convertit un classeur officiel de formats ATIH en descriptifs Pinel.\n" +
        "  pinel roles <dossier-descriptifs>\n" +
        "      Liste les champs que la pseudonymisation transforme, format par format.\n" +
        "  pinel anonymiser <dossier-source> <dossier-cible> <dossier-descriptifs>\n" +
        "      Produit une copie pseudonymisee des fichiers PMSI reconnus.\n" +
        "  pinel controler <dossier>\n" +
        "      Passe les controles qualite et affiche un decompte par code d'anomalie.\n" +
        "  pinel apprendre <dossier> <regles.json> <dossier-descriptifs>\n" +
        "      Tire les corrections regulieres du DIM des paires origine / corrige.\n" +
        "  pinel regles <regles.json>\n" +
        "      Liste les regles apprises, leur confiance et leur statut.\n" +
        "  pinel regle <regles.json> <numero> valider|rejeter|proposer\n" +
        "      Decision du DIM sur une regle : seules les regles validees corrigent une copie.\n" +
        "  pinel suggerer <dossier> <regles.json> <dossier-descriptifs> [dossier-copies]\n" +
        "      Decompte les lignes d'un lot concernees par les regles ; ecrit les copies corrigees.\n" +
        "  pinel entrainer <dossier> <dossier-modele> <dossier-descriptifs>\n" +
        "      Entraine le modele des suppressions RAA ; ne remplace le modele en place que s'il fait mieux.\n" +
        "  pinel scorer <dossier> <dossier-modele> <dossier-descriptifs> [rapport.csv]\n" +
        "      Signale les lignes RAA que le DIM supprimerait probablement.\n" +
        "  pinel ospi-extraire <dossier-source> <dossier-cible>\n" +
        "      Sort les lignes ATIH que transportent les exports JSON de la plateforme OSPI.";

    /// <summary>Verbes reconnus, pour qu'un lanceur sache si la ligne de commande lui est destinee.</summary>
    public static readonly IReadOnlySet<string> Verbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "formats-importer", "roles", "anonymiser", "controler", "apprendre", "regles", "regle",
        "suggerer", "entrainer", "scorer", "ospi-extraire",
    };

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
                "controler" when args.Length == 2 => CheckCommand.Run(args[1]),
                "apprendre" when args.Length == 4 => LearnCommand.Learn(args[1], args[2], args[3]),
                "regles" when args.Length == 2 => LearnCommand.Show(args[1]),
                "entrainer" when args.Length == 4 => ModelCommand.Train(args[1], args[2], args[3]),
                "scorer" when args.Length is 4 or 5 => ModelCommand.Score(args[1], args[2], args[3], args.Length == 5 ? args[4] : null),
                "regle" when args.Length == 4 => LearnCommand.SetStatus(args[1], args[2], args[3]),
                "suggerer" when args.Length is 4 or 5 => LearnCommand.Suggest(args[1], args[2], args[3], args.Length == 5 ? args[4] : null),
                "anonymiser" when args.Length == 4 => AnonymizeCommand.Run(args[1], args[2], args[3]),
                "ospi-extraire" when args.Length == 3 => OspiCommand.Run(args[1], args[2]),
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
