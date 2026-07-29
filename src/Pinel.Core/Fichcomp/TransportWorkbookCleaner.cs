using ClosedXML.Excel;

namespace Pinel.Core.Fichcomp;

/// <summary>Résultat du nettoyage d'un classeur transports.</summary>
public sealed record CleaningResult(
    string OutputPath,
    int RowsRead,
    int RowsRemoved,
    int DatesFilled);

/// <summary>
/// Nettoie les classeurs FICHCOMP transports produits par la chaîne de
/// facturation, qui répètent le bloc d'en-tête toutes les quelques lignes et
/// laissent la date de commande vide sur les lignes suivantes.
/// </summary>
/// <remarks>
/// <para>Règles reprises de la moulinette utilisée au DIM :</para>
/// <list type="bullet">
///   <item>les lignes qui répètent le bloc d'en-tête sont retirées de la
///     version nettoyée ;</item>
///   <item>la date de la deuxième colonne est propagée vers les lignes
///     suivantes tant qu'elles sont vides ;</item>
///   <item>le classeur produit contient deux feuilles : la version nettoyée,
///     et l'original où les lignes retirées sont marquées en rouge ;</item>
///   <item>le fichier d'origine n'est jamais modifié.</item>
/// </list>
/// </remarks>
public static class TransportWorkbookCleaner
{
    /// <summary>Intitulés du bloc d'en-tête, tels qu'ils apparaissent dans l'export.</summary>
    public static readonly string[] HeaderLabels =
    {
        "Date commande",
        "UF",
        "Libellé - Uf",
        "Date paiement - Mnd",
        "Date de naissance",
        "Nom fournisseur",
        "Adresse fournisseur ligne 1",
        "Code postal fournisseur",
        "Ville fournisseur",
        "Nombre de kilomètres",
    };

    private static readonly XLColor RemovedRowColor = XLColor.FromArgb(0xFF, 0xE5, 0xE5);
    private static readonly XLColor RemovedTextColor = XLColor.FromArgb(0xC0, 0x1A, 0x1A);

    /// <summary>
    /// Produit une copie nettoyée à côté du fichier d'origine, suffixée
    /// <c>_clean</c>. Si la copie existe déjà, elle est remplacée.
    /// </summary>
    public static CleaningResult Clean(string inputPath, string? outputPath = null)
    {
        outputPath ??= Path.Combine(
            Path.GetDirectoryName(inputPath) ?? ".",
            Path.GetFileNameWithoutExtension(inputPath) + "_clean.xlsx");

        using var source = new XLWorkbook(inputPath);

        // La feuille de donnees n'est pas toujours la premiere : on retient
        // celle qui porte le bloc d'en-tete attendu, sinon on refuse plutot que
        // de produire un classeur vide qui aurait l'air correct.
        var sheet = source.Worksheets.FirstOrDefault(HasExpectedHeader)
                    ?? throw new InvalidOperationException(
                        "aucune feuille ne porte le bloc d'en-tete FICHCOMP transports attendu");

        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        var lastColumn = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;

        using var output = new XLWorkbook();
        var clean = output.Worksheets.Add("Nettoye");
        var original = output.Worksheets.Add("Original");

        int cleanRow = 0, removed = 0, datesFilled = 0;
        string? lastDate = null;

        for (int row = 1; row <= lastRow; row++)
        {
            var values = new List<string>(lastColumn);
            for (int col = 1; col <= lastColumn; col++)
            {
                values.Add(sheet.Cell(row, col).GetFormattedString().Trim());
            }

            // Copie intégrale dans la feuille Original.
            for (int col = 1; col <= lastColumn; col++)
            {
                original.Cell(row, col).Value = values[col - 1];
            }

            var isHeaderRepeat = IsHeaderRow(values) && row > 1;
            if (isHeaderRepeat)
            {
                removed++;
                var range = original.Range(row, 1, row, Math.Max(lastColumn, 1));
                range.Style.Fill.BackgroundColor = RemovedRowColor;
                range.Style.Font.FontColor = RemovedTextColor;
                continue;
            }

            // Propagation de la date de la colonne B.
            if (lastColumn >= 2)
            {
                var date = values[1];
                if (date.Length == 0 && lastDate is { Length: > 0 } && row > 1)
                {
                    values[1] = lastDate;
                    datesFilled++;
                }
                else if (date.Length > 0)
                {
                    lastDate = date;
                }
            }

            cleanRow++;
            for (int col = 1; col <= lastColumn; col++)
            {
                clean.Cell(cleanRow, col).Value = values[col - 1];
            }
        }

        if (cleanRow > 0 && lastColumn > 0)
        {
            clean.Row(1).Style.Font.Bold = true;
            clean.Columns(1, lastColumn).AdjustToContents();
            original.Columns(1, lastColumn).AdjustToContents();
        }

        output.SaveAs(outputPath);
        return new CleaningResult(outputPath, lastRow, removed, datesFilled);
    }

    /// <summary>
    /// Vrai si la feuille porte le bloc d'en-tête dans ses dix premières lignes.
    /// </summary>
    private static bool HasExpectedHeader(IXLWorksheet sheet)
    {
        var lastColumn = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        if (lastColumn == 0) return false;

        var lastRow = Math.Min(sheet.LastRowUsed()?.RowNumber() ?? 0, 10);
        for (int row = 1; row <= lastRow; row++)
        {
            var values = new List<string>(lastColumn);
            for (int col = 1; col <= lastColumn; col++)
            {
                values.Add(sheet.Cell(row, col).GetFormattedString().Trim());
            }
            if (IsHeaderRow(values)) return true;
        }
        return false;
    }

    /// <summary>
    /// Vrai quand la ligne reprend au moins la moitié des intitulés du bloc
    /// d'en-tête : les exports ne répètent pas toujours toutes les colonnes.
    /// </summary>
    internal static bool IsHeaderRow(IReadOnlyList<string> values)
    {
        int matches = 0;
        foreach (var label in HeaderLabels)
        {
            if (values.Any(v => string.Equals(v, label, StringComparison.OrdinalIgnoreCase)))
            {
                matches++;
            }
        }
        return matches >= HeaderLabels.Length / 2;
    }
}
