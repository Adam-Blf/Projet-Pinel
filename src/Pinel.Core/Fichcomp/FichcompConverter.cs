using System.Globalization;
using System.Text;
using Pinel.Core.Formats;

namespace Pinel.Core.Fichcomp;

/// <summary>
/// Valeur qui ne tient pas dans la largeur du format. Elle est signalée plutôt
/// que tronquée ou saturée : un fichier transmis aux tutelles ne doit jamais
/// contenir une donnée plausible mais fausse.
/// </summary>
public sealed class FichcompOverflowException : Exception
{
    public FichcompOverflowException(string message) : base(message) { }
}

/// <summary>Anomalie relevée sur une ligne de fichier complémentaire.</summary>
public sealed record FichcompIssue(int LineNumber, string Message);

/// <summary>Résultat d'une conversion ou d'un contrôle.</summary>
public sealed record FichcompResult(int Lines, IReadOnlyList<FichcompIssue> Issues)
{
    public bool IsClean => Issues.Count == 0;
}

/// <summary>
/// Conversion dans les deux sens entre les lignes de fichier complémentaire à
/// largeur fixe et des enregistrements exploitables, plus le contrôle de
/// conformité des lignes.
/// </summary>
/// <remarks>
/// Reprise de la moulinette Excel utilisée au DIM, portée ici pour que le DIM
/// n'ait plus qu'un seul outil : les mêmes règles de remplissage, les mêmes
/// longueurs, les mêmes contrôles.
/// </remarks>
public static class FichcompConverter
{


    /// <summary>
    /// Met une valeur en forme sur une largeur fixe. Une valeur trop longue
    /// leve : la tronquer produirait deux sejours differents ecrits a
    /// l'identique dans le fichier transmis.
    /// </summary>
    internal static string Pad(string? value, int length, bool zeroPadLeft, string field = "champ")
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length > length)
        {
            throw new FichcompOverflowException(
                $"{field} : {text.Length} caracteres pour une largeur de {length}");
        }
        return zeroPadLeft ? text.PadLeft(length, '0') : text.PadRight(length);
    }

    /// <summary>Quantité mise à l'échelle, par exemple 1,5 sur 7 caractères au millième donne 0001500.</summary>
    internal static string FormatQuantity(decimal quantity, FichcompLayout layout)
    {
        var factor = (decimal)Math.Pow(10, layout.QuantityDecimals);
        var scaled = (long)Math.Round(quantity * factor, MidpointRounding.AwayFromZero);
        if (scaled < 0) scaled = 0;
        var text = scaled.ToString(CultureInfo.InvariantCulture);
        if (text.Length > layout.QuantityLength)
        {
            // Saturer a 9999999 donnerait une quantite plausible et fausse.
            throw new FichcompOverflowException(
                $"quantite {quantity} hors capacite du format, {layout.QuantityLength} caracteres");
        }
        return text.PadLeft(layout.QuantityLength, '0');
    }

    /// <summary>Date au format JJMMAAAA, ou huit espaces quand elle est absente.</summary>
    internal static string FormatDate(DateOnly? date) =>
        date is null ? new string(' ', 8) : date.Value.ToString("ddMMyyyy", CultureInfo.InvariantCulture);

    /// <summary>Construit la ligne à largeur fixe d'un enregistrement.</summary>
    public static string ToLine(FichcompRecord record, FichcompLayout layout)
    {
        var builder = new StringBuilder(layout.TotalLength);
        builder.Append(Pad(record.Finess, layout.FinessLength, zeroPadLeft: true, field: "FINESS"));
        builder.Append(Pad(record.StayNumber, layout.StayLength, zeroPadLeft: false, field: "numero de sejour"));
        builder.Append(Pad(record.Code, layout.CodeLength, zeroPadLeft: true, field: "code"));
        builder.Append(FormatQuantity(record.Quantity, layout));
        builder.Append(FormatDate(record.Date));
        return builder.ToString();
    }

    /// <summary>Écrit un fichier complémentaire à partir d'enregistrements.</summary>
    public static int Write(IEnumerable<FichcompRecord> records, string outputPath, FichcompLayout layout)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        using var writer = new StreamWriter(outputPath, append: false, PmsiEncoding.Latin1);

        int count = 0;
        foreach (var record in records)
        {
            writer.WriteLine(ToLine(record, layout));
            count++;
        }
        return count;
    }

    /// <summary>Relit un fichier complémentaire vers des enregistrements.</summary>
    public static IReadOnlyList<FichcompRecord> Read(string path, FichcompLayout layout)
    {
        var records = new List<FichcompRecord>();
        using var reader = new StreamReader(path, PmsiEncoding.Latin1);

        while (reader.ReadLine() is { } line)
        {
            if (line.Trim().Length == 0) continue;
            records.Add(Parse(line, layout));
        }

        return records;
    }

    internal static FichcompRecord Parse(string line, FichcompLayout layout)
    {
        string Slice(int start, int length) =>
            start >= line.Length ? string.Empty : line.Substring(start, Math.Min(length, line.Length - start));

        var quantityText = Slice(layout.QuantityStart, layout.QuantityLength).Trim();
        decimal quantity = 0m;
        if (long.TryParse(quantityText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var scaled))
        {
            quantity = scaled / (decimal)Math.Pow(10, layout.QuantityDecimals);
        }

        var dateText = Slice(layout.DateStart, layout.DateLength).Trim();
        DateOnly? date = null;
        if (DateOnly.TryParseExact(dateText, "ddMMyyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            date = parsed;
        }

        return new FichcompRecord(
            Finess: Slice(layout.FinessStart, layout.FinessLength).Trim(),
            StayNumber: Slice(layout.StayStart, layout.StayLength).Trim(),
            Code: Slice(layout.CodeStart, layout.CodeLength).Trim(),
            Quantity: quantity,
            Date: date);
    }

    /// <summary>
    /// Contrôle un fichier complémentaire : longueur de ligne, FINESS et code
    /// numériques, quantité numérique, date au format attendu.
    /// </summary>
    public static FichcompResult Check(string path, FichcompLayout layout)
    {
        var issues = new List<FichcompIssue>();
        int lineNumber = 0, lines = 0;

        using var reader = new StreamReader(path, PmsiEncoding.Latin1);
        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            if (line.Trim().Length == 0) continue;
            lines++;

            if (line.Length != layout.TotalLength)
            {
                issues.Add(new FichcompIssue(lineNumber,
                    $"longueur {line.Length}, attendue {layout.TotalLength}"));

                // Ligne trop courte : les controles de champ n'auraient aucun
                // sens et noieraient la vraie anomalie sous des faux positifs.
                if (line.Length < layout.TotalLength) continue;
            }

            var finess = line.Length >= layout.FinessLength ? line[..layout.FinessLength] : line;
            if (!finess.All(char.IsDigit))
            {
                issues.Add(new FichcompIssue(lineNumber, "FINESS non numérique"));
            }

            if (line.Length >= layout.CodeStart + layout.CodeLength)
            {
                var code = line.Substring(layout.CodeStart, layout.CodeLength).Trim();
                if (code.Length == 0 || !code.All(char.IsDigit))
                {
                    issues.Add(new FichcompIssue(lineNumber, "code non numérique"));
                }
            }

            if (line.Length >= layout.QuantityStart + layout.QuantityLength)
            {
                var quantity = line.Substring(layout.QuantityStart, layout.QuantityLength);
                if (!quantity.All(char.IsDigit))
                {
                    issues.Add(new FichcompIssue(lineNumber, "quantité non numérique"));
                }
            }

            if (line.Length >= layout.DateStart + layout.DateLength)
            {
                var date = line.Substring(layout.DateStart, layout.DateLength);
                if (date.Trim().Length != 0
                    && !DateOnly.TryParseExact(date, "ddMMyyyy", CultureInfo.InvariantCulture,
                                               DateTimeStyles.None, out _))
                {
                    issues.Add(new FichcompIssue(lineNumber, "date invalide, attendu JJMMAAAA"));
                }
            }
        }

        return new FichcompResult(lines, issues);
    }
}
