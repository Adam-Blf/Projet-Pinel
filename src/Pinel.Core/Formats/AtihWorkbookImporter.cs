using System.Globalization;
using System.Text;
using ClosedXML.Excel;

namespace Pinel.Core.Formats;

/// <summary>
/// Convertit un classeur officiel de formats ATIH (formats_psy_AAAA.xlsx,
/// formats_mco_AAAA.xlsx...) en descriptifs Pinel, un par feuille.
/// </summary>
/// <remarks>
/// <para>
/// Chaque annee, l'ATIH publie ses formats sous forme de classeur Excel. Sans
/// cet import, le DIM devait recopier les positions a la main dans un fichier
/// <c>*.format.csv</c>. L'import lit les feuilles telles qu'elles sont publiees :
/// </para>
/// <list type="bullet">
///   <item>la ligne d'en-tete est reperee par ses intitules (Taille, Debut, Fin),
///     sa position varie d'une feuille a l'autre ;</item>
///   <item>l'ordre des colonnes varie aussi (Taille avant Debut sur les RPS,
///     apres Fin sur les RSF) : chaque colonne est retrouvee par son nom ;</item>
///   <item>les positions ecrites en formule (VID-HOSP) sont lues par leur valeur
///     calculee, et a defaut deduites de la fin du champ precedent ;</item>
///   <item>seule la partie fixe est importee : la premiere ligne qui porte une
///     taille sans position ouvre la zone repetee (diagnostics associes, actes),
///     dont la longueur depend de compteurs portes par la ligne.</item>
/// </list>
/// </remarks>
public static class AtihWorkbookImporter
{
    /// <summary>
    /// Nom Pinel des feuilles connues. Une feuille absente de la table garde
    /// un nom derive de son intitule.
    /// </summary>
    private static readonly (string Prefix, string Format)[] SheetAliases =
    {
        ("RPS", "RPS"),
        ("RAA", "RAA"),
        ("FICHCOMP ISOLEMENT", "FICHCOMP-ISO"),
        ("FICHCOMP TEMPS PARTIEL", "FICHCOMP-TP"),
        ("FICHCOMP TRANSPORTS", "FICHCOMP"),
        ("FICHIER DES UM", "FICUM-PSY"),
        ("VID-HOSP", "VID-HOSP"),
        ("VID-IPP", "VID-IPP"),
        ("VID-CHAINAGE", "VID-CHAINAGE"),
        ("HOSP-PMSI", "HOSP-PMSI"),
        ("HOSP-FACT", "HOSP-FACT"),
    };

    /// <summary>
    /// Lit toutes les feuilles du classeur et rend un descriptif par feuille
    /// dont au moins un champ a pu etre lu.
    /// </summary>
    public static IReadOnlyList<FormatLayout> Import(string workbookPath, int year, string? domain = null)
    {
        using var workbook = new XLWorkbook(workbookPath);
        var layouts = new List<FormatLayout>();
        foreach (var sheet in workbook.Worksheets)
        {
            var (fields, repeatZone) = ReadSheet(sheet);
            if (fields.Count == 0) continue;
            layouts.Add(new FormatLayout(FormatName(sheet.Name, domain), year, fields, workbookPath, repeatZone));
        }
        return layouts;
    }

    /// <summary>
    /// Ecrit chaque descriptif en <c>FORMAT.ANNEE.format.csv</c> dans
    /// <paramref name="outputDirectory"/>. Rend les chemins ecrits.
    /// </summary>
    public static IReadOnlyList<string> WriteAll(IEnumerable<FormatLayout> layouts, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var written = new List<string>();
        foreach (var layout in layouts)
        {
            var path = Path.Combine(outputDirectory, $"{layout.Format}.{layout.Year}.format.csv");
            var sb = new StringBuilder();
            sb.AppendLine($"# format: {layout.Format}");
            if (layout.Year is int y) sb.AppendLine($"# annee: {y}");
            sb.AppendLine($"# source: {Path.GetFileName(layout.Source)}");
            if (layout.HasRepeatZone) sb.AppendLine("# zone-repetee: oui");
            sb.AppendLine("nom;debut;longueur;libelle");
            foreach (var field in layout.Fields)
            {
                sb.AppendLine($"{field.Name};{field.Start};{field.Length};{field.Label.Replace(';', ',')}");
            }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            written.Add(path);
        }
        return written;
    }

    internal static string FormatName(string sheetName, string? domain)
    {
        var key = Normalize(sheetName);
        foreach (var (prefix, format) in SheetAliases)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
            {
                // Le fichier des UM existe en PSY et en MCO, sous deux formats.
                return format == "FICUM-PSY" && domain is { } d && !d.Equals("PSY", StringComparison.OrdinalIgnoreCase)
                    ? $"FICUM-{d.ToUpperInvariant()}"
                    : format;
            }
        }
        var slug = Slug(sheetName);
        return domain is { Length: > 0 } ? $"{slug}-{domain.ToUpperInvariant()}" : slug;
    }

    private static (List<FormatField> Fields, bool RepeatZone) ReadSheet(IXLWorksheet sheet)
    {
        var used = sheet.RangeUsed();
        if (used is null) return (new List<FormatField>(), false);

        int lastRow = used.LastRow().RowNumber();
        int lastCol = Math.Min(used.LastColumn().ColumnNumber(), 20);

        // Repere la ligne d'en-tete : elle porte "Taille" et "Debut".
        int headerRow = -1, colSize = -1, colStart = -1, colLabel = -1;
        for (int r = 1; r <= Math.Min(lastRow, 15) && headerRow < 0; r++)
        {
            int size = -1, start = -1, label = -1;
            for (int c = 1; c <= lastCol; c++)
            {
                var text = Normalize(Text(sheet.Cell(r, c)));
                if (text == "TAILLE") size = c;
                else if (text.StartsWith("DEBUT", StringComparison.Ordinal)) start = c;
                else if (label < 0 && (text.StartsWith("LIBELLE", StringComparison.Ordinal) || text == "NOM")) label = c;
            }
            if (size > 0 && start > 0)
            {
                headerRow = r; colSize = size; colStart = start; colLabel = label > 0 ? label : 1;
            }
        }
        if (headerRow < 0) return (new List<FormatField>(), false);

        var fields = new List<FormatField>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        int previousEnd = 0;
        for (int r = headerRow + 1; r <= lastRow; r++)
        {
            var label = Text(sheet.Cell(r, colLabel));
            // Sous-libelle en colonne suivante (actes CCAM des RPS) quand la
            // colonne principale est vide.
            if (label.Length == 0 && colLabel + 1 != colSize && colLabel + 1 != colStart)
            {
                label = Text(sheet.Cell(r, colLabel + 1));
            }

            int? size = Number(sheet.Cell(r, colSize));
            int? start = Number(sheet.Cell(r, colStart));

            if (size is null or < 1)
            {
                if (label.Length == 0 && fields.Count > 0) break; // fin du tableau
                continue;
            }

            // Taille sans position apres des champs fixes : zone repetee.
            if (start is null && fields.Count > 0)
            {
                return (fields, true);
            }

            int position = start is int s && s > 0 ? s : previousEnd + 1;
            var name = UniqueName(Slug(label.Length > 0 ? label : $"CHAMP_{position}"), names);
            fields.Add(new FormatField(name, position, size.Value, CleanLabel(label)));
            previousEnd = position + size.Value - 1;
        }
        return (fields, false);
    }

    private static string Text(IXLCell cell)
    {
        var value = cell.HasFormula ? cell.CachedValue : cell.Value;
        return value.ToString(CultureInfo.InvariantCulture).Trim();
    }

    private static int? Number(IXLCell cell)
    {
        var value = cell.HasFormula ? cell.CachedValue : cell.Value;
        if (value.IsNumber) return (int)Math.Round(value.GetNumber());
        var text = value.ToString(CultureInfo.InvariantCulture).Trim().TrimEnd('-').Trim();
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    private static string CleanLabel(string label) =>
        string.Join(' ', label.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string UniqueName(string name, HashSet<string> names)
    {
        var candidate = name;
        for (int i = 2; !names.Add(candidate); i++) candidate = $"{name}_{i}";
        return candidate;
    }

    /// <summary>Majuscules sans accents, pour comparer des intitules.</summary>
    public static string Normalize(string text)
    {
        var decomposed = text.Trim().ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        }
        return string.Join(' ', sb.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Nom de colonne : majuscules, chiffres et soulignes, 40 caracteres au plus.</summary>
    internal static string Slug(string text)
    {
        var sb = new StringBuilder();
        foreach (var ch in Normalize(text))
        {
            if (char.IsAsciiLetterOrDigit(ch)) sb.Append(ch);
            else if (sb.Length > 0 && sb[^1] != '_') sb.Append('_');
        }
        var slug = sb.ToString().Trim('_');
        return slug.Length > 40 ? slug[..40].TrimEnd('_') : slug;
    }
}
