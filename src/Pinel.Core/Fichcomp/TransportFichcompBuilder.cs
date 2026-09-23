using System.Globalization;
using ClosedXML.Excel;

namespace Pinel.Core.Fichcomp;

/// <summary>Résultat de la construction de la feuille Fichcomp transports.</summary>
public sealed record TransportFichcompBuildResult(
    int RowsWritten,
    IReadOnlyList<FichcompIssue> Issues)
{
    public bool IsClean => Issues.Count == 0;
}

/// <summary>
/// Construit la feuille "Fichcomp" à partir de la feuille "Rapport 1 modifié"
/// d'un classeur transports déjà nettoyé par <see cref="TransportWorkbookCleaner"/>.
/// </summary>
/// <remarks>
/// Mapping relevé dans les formules du classeur cible : la ligne 2 de
/// Fichcomp correspond à la ligne <see cref="TransportFichcompLayout.SourceFirstDataRow"/>
/// de la feuille source, puis les lignes se correspondent une à une. Les
/// lignes source entièrement vides sont ignorées plutôt que reproduites en
/// blanc, ce qui gère naturellement la queue de la feuille source sans casser
/// la correspondance des lignes qui portent des données.
/// </remarks>
public static class TransportFichcompBuilder
{
    private const int SourceColumnDateCommande = 2;  // B
    private const int SourceColumnUf = 3;             // C
    private const int SourceColumnLibelleUf = 4;      // D
    private const int SourceColumnColonneF = 6;       // F
    private const int SourceColumnDateNaissance = 7;  // G
    private const int SourceColumnNomFournisseur = 8; // H
    private const int SourceColumnAdresse = 9;        // I
    private const int SourceColumnCodePostal = 10;    // J
    private const int SourceColumnVille = 11;          // K
    private const int SourceColumnKilometres = 12;    // L

    /// <summary>
    /// Écrit l'en-tête et les lignes de la feuille Fichcomp dans
    /// <paramref name="target"/> à partir des données de <paramref name="source"/>.
    /// La feuille cible est supposée vide : cette méthode ne fait qu'y ajouter.
    /// </summary>
    public static TransportFichcompBuildResult Build(
        IXLWorksheet source,
        IXLWorksheet target,
        TransportFichcompOptions options,
        int sourceFirstDataRow = TransportFichcompLayout.SourceFirstDataRow)
    {
        WriteHeader(target);

        var lastSourceRow = source.LastRowUsed()?.RowNumber() ?? 0;
        var issues = new List<FichcompIssue>();
        int written = 0;

        for (int row = sourceFirstDataRow; row <= lastSourceRow; row++)
        {
            var fields = ReadRow(source, row);
            if (!fields.HasData) continue;

            var targetRow = 2 + (row - sourceFirstDataRow);
            WriteRow(target, targetRow, fields, options, row, issues);
            written++;
        }

        return new TransportFichcompBuildResult(written, issues);
    }

    private static void WriteHeader(IXLWorksheet target)
    {
        for (int col = 0; col < TransportFichcompLayout.ColumnHeaders.Count; col++)
        {
            target.Cell(1, col + 1).Value = TransportFichcompLayout.ColumnHeaders[col];
        }
        target.Row(1).Style.Font.Bold = true;
    }

    private readonly record struct SourceFields(
        string Uf,
        string LibelleUf,
        string ColonneF,
        string DateNaissance,
        string NomFournisseur,
        string Adresse,
        string CodePostal,
        string Ville,
        string Kilometres,
        DateOnly? DateTransport,
        bool HasData);

    private static SourceFields ReadRow(IXLWorksheet source, int row)
    {
        string Text(int col) => source.Cell(row, col).GetFormattedString().Trim();

        var uf = Text(SourceColumnUf);
        var libelleUf = Text(SourceColumnLibelleUf);
        var colonneF = Text(SourceColumnColonneF);
        var dateNaissance = Text(SourceColumnDateNaissance);
        var nomFournisseur = Text(SourceColumnNomFournisseur);
        var adresse = Text(SourceColumnAdresse);
        var codePostal = Text(SourceColumnCodePostal);
        var ville = Text(SourceColumnVille);
        var kilometres = Text(SourceColumnKilometres);
        var dateTransport = TryParseDate(source.Cell(row, SourceColumnDateCommande));

        var hasData = uf.Length > 0 || libelleUf.Length > 0 || colonneF.Length > 0
            || dateNaissance.Length > 0 || nomFournisseur.Length > 0 || adresse.Length > 0
            || codePostal.Length > 0 || ville.Length > 0 || kilometres.Length > 0
            || dateTransport is not null;

        return new SourceFields(uf, libelleUf, colonneF, dateNaissance, nomFournisseur,
            adresse, codePostal, ville, kilometres, dateTransport, hasData);
    }

    private static void WriteRow(
        IXLWorksheet target,
        int targetRow,
        SourceFields fields,
        TransportFichcompOptions options,
        int sourceRow,
        List<FichcompIssue> issues)
    {
        target.Cell(targetRow, 1).Value = fields.Uf;                          // A UF
        target.Cell(targetRow, 2).Value = fields.LibelleUf;                   // B Libelle - Uf
        target.Cell(targetRow, 3).Value = fields.ColonneF;                    // C
        target.Cell(targetRow, 4).Value = fields.DateNaissance;               // D Date de naissance
        // E IPP : laissee vide, colonne a ajouter par le DIM.
        target.Cell(targetRow, 6).Value = options.FinessEPmsi;                // F Finess e-PMSI
        target.Cell(targetRow, 7).Value = options.TypeDePrestation;           // G Type de prestation
        // H NDA : laissee vide.
        target.Cell(targetRow, 9).Value = options.FinessGeographique;         // I Numero FINESS geographique

        if (fields.DateTransport is { } transportDate)
        {
            target.Cell(targetRow, 10).Value = transportDate.ToDateTime(TimeOnly.MinValue); // J
        }

        target.Cell(targetRow, 11).Value = options.CodeForfait;               // K Code forfait
        target.Cell(targetRow, 12).Value = options.ClasseDistance;            // L Classe de distance

        if (fields.DateTransport is { } date)
        {
            try
            {
                target.Cell(targetRow, 13).Value = TransportFichcompLayout.ComposeLine(
                    fields.Uf, fields.LibelleUf, fields.ColonneF, fields.DateNaissance, date, options); // M
            }
            catch (FichcompOverflowException ex)
            {
                issues.Add(new FichcompIssue(sourceRow,
                    $"ligne FICHCOMP (colonne M) non composee, Rapport 1 modifie ligne {sourceRow} : {ex.Message}"));
            }
        }
        else
        {
            issues.Add(new FichcompIssue(sourceRow,
                $"ligne FICHCOMP (colonne M) non composee, Rapport 1 modifie ligne {sourceRow} : date transp. aller absente ou illisible en colonne B"));
        }

        target.Cell(targetRow, 14).Value = fields.Kilometres;                 // N Nombre de kilometres
        // O Commentaire : laissee vide.
        target.Cell(targetRow, 16).Value = fields.NomFournisseur;             // P
        target.Cell(targetRow, 17).Value = fields.Adresse;                    // Q
        target.Cell(targetRow, 18).Value = fields.CodePostal;                 // R
        target.Cell(targetRow, 19).Value = fields.Ville;                      // S
    }

    private static DateOnly? TryParseDate(IXLCell cell)
    {
        if (cell.TryGetValue(out DateTime dateTimeValue))
        {
            return DateOnly.FromDateTime(dateTimeValue);
        }

        var text = cell.GetFormattedString().Trim();
        if (text.Length == 0) return null;

        if (DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var invariant))
        {
            return invariant;
        }

        if (DateOnly.TryParse(text, CultureInfo.GetCultureInfo("fr-FR"), DateTimeStyles.None, out var french))
        {
            return french;
        }

        return null;
    }
}
