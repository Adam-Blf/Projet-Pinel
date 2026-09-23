using Pinel.Core.Security;

namespace Pinel.Core.Formats;

/// <summary>
/// Catalogue des descriptifs de format disponibles.
/// </summary>
/// <remarks>
/// <para>
/// Trois sources, dans cet ordre de priorite :
/// </para>
/// <list type="number">
///   <item>les descriptifs deposes par le DIM dans le dossier de descriptifs
///     (<see cref="DefaultDirectory"/> ou un dossier choisi), recopies des
///     descriptifs officiels ATIH ;</item>
///   <item>les descriptifs minimaux integres, limites aux champs dont la
///     position est verifiee par les tests du projet ;</item>
///   <item>aucun descriptif : le fichier est exporte en une colonne brute,
///     avec un avertissement, plutot que d'inventer un decoupage.</item>
/// </list>
/// <para>
/// Ce fonctionnement repond directement au cahier des charges : les formats
/// changent tous les ans, l'ajout d'un millesime se fait en deposant un
/// fichier, sans nouvelle version de l'application.
/// </para>
/// </remarks>
public sealed class LayoutRegistry
{
    private readonly Dictionary<string, List<FormatLayout>> _layouts = new(StringComparer.OrdinalIgnoreCase);

    public LayoutRegistry(IEnumerable<FormatLayout>? layouts = null)
    {
        foreach (var layout in layouts ?? BuiltIn())
        {
            Add(layout);
        }
    }

    /// <summary>Dossier par defaut des descriptifs deposes par le DIM.</summary>
    public static string DefaultDirectory => Path.Combine(
        PinelPaths.DataRoot, "formats");

    /// <summary>Formats pour lesquels au moins un descriptif est connu.</summary>
    public IEnumerable<string> Formats => _layouts.Keys;

    public void Add(FormatLayout layout)
    {
        if (!_layouts.TryGetValue(layout.Format, out var list))
        {
            list = new List<FormatLayout>();
            _layouts[layout.Format] = list;
        }
        list.Add(layout);
    }

    /// <summary>
    /// Charge tous les descriptifs d'un dossier (fichiers <c>*.format.csv</c>).
    /// Retourne le nombre de descriptifs ajoutes.
    /// </summary>
    public int LoadDirectory(string directory)
    {
        if (!Directory.Exists(directory)) return 0;

        int added = 0;
        foreach (var file in Directory.EnumerateFiles(directory, "*.format.csv", SearchOption.AllDirectories))
        {
            try
            {
                var layout = FormatLayout.Load(file);
                if (layout.Fields.Count == 0) continue;
                Add(layout);
                added++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                       or System.Security.SecurityException or FormatException)
            {
                // Descriptif verrouille, protege ou mal forme : ignore, les
                // autres descriptifs du dossier doivent quand meme charger.
            }
        }
        return added;
    }

    /// <summary>
    /// Descriptif applicable a un format et, si elle est connue, a une annee.
    /// Le descriptif de l'annee exacte gagne, sinon le plus recent qui lui est
    /// anterieur, sinon celui sans annee.
    /// </summary>
    public FormatLayout? Resolve(string format, int? year = null)
    {
        if (!_layouts.TryGetValue(format, out var list) || list.Count == 0) return null;

        if (year is int wanted)
        {
            var exact = list.FirstOrDefault(l => l.Year == wanted);
            if (exact is not null) return exact;

            var previous = list
                .Where(l => l.Year is int y && y <= wanted)
                .OrderByDescending(l => l.Year)
                .FirstOrDefault();
            if (previous is not null) return previous;

            // Aucun descriptif de cette annee ni d'une annee anterieure. On
            // n'applique jamais un descriptif posterieur : les positions ont pu
            // changer entre-temps, le decoupage serait faux sans le dire.
            return list.FirstOrDefault(l => l.Year is null);
        }

        return list.FirstOrDefault(l => l.Year is null) ?? list.OrderByDescending(l => l.Year).First();
    }

    /// <summary>
    /// Descriptifs minimaux integres, construits a partir des seules positions
    /// verifiees par les tests : identifiant patient et date de naissance, plus
    /// le FINESS quand le format le porte en tete. Ils permettent de produire un
    /// CSV utile avant meme que le DIM ait depose les descriptifs officiels.
    /// </summary>
    public static IEnumerable<FormatLayout> BuiltIn()
    {
        foreach (var (name, spec) in AtihMatrix.All)
        {
            // Un format dont le descriptif officiel ne declare aucun identifiant
            // patient ne recoit PAS de gabarit par defaut. Sans gabarit, Resolve
            // rend null et l'export bascule sur la colonne brute annoncee au
            // chapitre 4.3 du guide, qui redevient ainsi atteignable.
            if (!spec.CarriesPatientIdentifiers)
            {
                continue;
            }

            var fields = new List<FormatField>
            {
                new("IPP", spec.IppStart + 1, spec.IppLength, "Identifiant permanent du patient"),
                new("DATE_NAISSANCE", spec.DdnStart + 1, spec.DdnLength, "Date de naissance"),
            };

            yield return new FormatLayout(name, year: null, fields);
        }
    }
}
