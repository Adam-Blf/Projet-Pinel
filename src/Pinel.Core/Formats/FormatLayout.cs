using System.Globalization;
using System.Text;

namespace Pinel.Core.Formats;

/// <summary>
/// Un champ d'un format ATIH a largeur fixe.
/// </summary>
/// <param name="Name">Nom court, sert d'en-tete de colonne dans les exports CSV.</param>
/// <param name="Start">Position du premier caractere, numerotee a partir de 1 comme dans les descriptifs ATIH.</param>
/// <param name="Length">Longueur en caracteres.</param>
/// <param name="Label">Libelle lisible, facultatif.</param>
public sealed record FormatField(string Name, int Start, int Length, string Label = "")
{
    /// <summary>Index 0 dans la chaine, pour le decoupage.</summary>
    public int Offset => Start - 1;

    public int End => Offset + Length;

    /// <summary>Extrait le champ d'une ligne, chaine vide si la ligne est trop courte.</summary>
    public string Read(string line)
    {
        if (Offset >= line.Length) return string.Empty;
        var length = Math.Min(Length, line.Length - Offset);
        return line.Substring(Offset, length).Trim();
    }
}

/// <summary>
/// Descriptif positionnel d'un format, c'est-a-dire la liste ordonnee de ses
/// champs. C'est ce descriptif qui pilote la moulinette : un fichier ATIH
/// devient un CSV avec une colonne par champ declare.
/// </summary>
/// <remarks>
/// <para>
/// Les formats ATIH changent tous les ans. Les descriptifs ne sont donc pas
/// figes dans le code : ils se chargent depuis des fichiers texte que le DIM
/// depose lui-meme, recopies des descriptifs officiels. Ajouter une annee ou
/// un format ne demande aucune recompilation.
/// </para>
/// <para>
/// Format du fichier, encode en UTF-8, une ligne par champ :
/// <code>
/// # format: RPS
/// # annee: 2025
/// nom;debut;longueur;libelle
/// FINESS;1;9;FINESS de l'etablissement
/// IPP;22;20;Identifiant permanent du patient
/// </code>
/// Les positions se lisent comme dans les descriptifs ATIH, le premier
/// caractere de la ligne porte le numero 1.
/// </para>
/// </remarks>
public sealed class FormatLayout
{
    public FormatLayout(string format, int? year, IReadOnlyList<FormatField> fields, string source = "", bool hasRepeatZone = false)
    {
        Format = format;
        Year = year;
        Fields = fields;
        Source = source;
        HasRepeatZone = hasRepeatZone;
    }

    /// <summary>Nom du format ATIH, tel que reconnu par l'identification de fichier.</summary>
    public string Format { get; }

    /// <summary>Annee du descriptif, nulle quand il vaut pour toutes les annees.</summary>
    public int? Year { get; }

    /// <summary>Champs declares, dans l'ordre des colonnes de sortie.</summary>
    public IReadOnlyList<FormatField> Fields { get; }

    /// <summary>Chemin du fichier d'ou vient le descriptif, vide s'il est integre.</summary>
    public string Source { get; }

    /// <summary>
    /// Vrai quand le format se prolonge par une zone repetee (diagnostics
    /// associes, actes) dont la longueur depend d'un compteur de la ligne : les
    /// champs declares ne couvrent alors que la partie fixe.
    /// </summary>
    public bool HasRepeatZone { get; }

    /// <summary>Longueur minimale attendue d'une ligne complete.</summary>
    public int ExpectedLength => Fields.Count == 0 ? 0 : Fields.Max(f => f.End);

    /// <summary>Decoupe une ligne en valeurs, dans l'ordre des champs.</summary>
    public IReadOnlyList<string> Split(string line) =>
        Fields.Select(f => f.Read(line)).ToList();

    /// <summary>
    /// Lit un descriptif depuis un fichier texte. Les lignes vides et les
    /// lignes commencant par # sont ignorees, sauf les en-tetes
    /// <c># format:</c> et <c># annee:</c>.
    /// </summary>
    public static FormatLayout Load(string path)
    {
        var fields = new List<FormatField>();
        string? format = null;
        int? year = null;
        bool repeatZone = false;

        foreach (var raw in File.ReadAllLines(path, Encoding.UTF8))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            if (line.StartsWith('#'))
            {
                var comment = line.TrimStart('#').Trim();
                if (comment.StartsWith("format:", StringComparison.OrdinalIgnoreCase))
                {
                    format = comment[7..].Trim().ToUpperInvariant();
                }
                else if (comment.StartsWith("annee:", StringComparison.OrdinalIgnoreCase)
                         && int.TryParse(comment[6..].Trim(), out var parsedYear))
                {
                    year = parsedYear;
                }
                else if (comment.StartsWith("zone-repetee:", StringComparison.OrdinalIgnoreCase))
                {
                    repeatZone = comment[13..].Trim().Equals("oui", StringComparison.OrdinalIgnoreCase);
                }
                continue;
            }

            var parts = line.Split(';');
            if (parts.Length < 3) continue;
            if (string.Equals(parts[0].Trim(), "nom", StringComparison.OrdinalIgnoreCase)) continue; // en-tete

            if (!int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var start)
                || !int.TryParse(parts[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var length)
                || start < 1 || length < 1)
            {
                continue;
            }

            fields.Add(new FormatField(
                Name: parts[0].Trim(),
                Start: start,
                Length: length,
                Label: parts.Length > 3 ? parts[3].Trim() : string.Empty));
        }

        format ??= Path.GetFileNameWithoutExtension(path).Split('.')[0].ToUpperInvariant();
        return new FormatLayout(format, year, fields, path, repeatZone);
    }
}
