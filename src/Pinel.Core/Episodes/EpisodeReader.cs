using System.Text;
using Pinel.Core.Formats;

namespace Pinel.Core.Episodes;

/// <summary>
/// Noms de champs cherches dans le descriptif de format pour reconstituer une
/// venue ambulatoire. Plusieurs libelles sont acceptes, les descriptifs ATIH
/// recopies par le DIM ne nomment pas toujours les colonnes de la meme facon.
/// </summary>
public sealed record EpisodeFieldNames(
    IReadOnlyList<string> Ipp,
    IReadOnlyList<string> Date,
    IReadOnlyList<string> Um,
    IReadOnlyList<string> Site)
{
    public static EpisodeFieldNames Default { get; } = new(
        Ipp: new[] { "IPP", "NUM_PATIENT", "ID_PATIENT" },
        Date: new[] { "DATE_ACTE", "DATE_VENUE", "DATE", "DATE_SEANCE" },
        Um: new[] { "UM", "NUM_UM", "UNITE_MEDICALE", "CODE_UM" },
        Site: new[] { "SITE", "FINESS", "FINESS_GEO", "ETABLISSEMENT" });
}

/// <summary>
/// Extrait les venues ambulatoires d'un fichier de recueil ambulatoire, RAA
/// ou R3A, a partir de son descriptif de format.
/// </summary>
/// <remarks>
/// Le descriptif doit declarer au minimum un champ d'identifiant patient et un
/// champ de date. Sans eux, le fichier est ignore : l'application prefere ne
/// rien produire plutot que produire des episodes faux.
/// Les formats d'hospitalisation complete, RPS en tete, sont volontairement
/// exclus : une duree de presence calculee sur un sejour complet n'aurait
/// aucun sens au regard de la demande du DIM.
/// </remarks>
public static class EpisodeReader
{


    /// <summary>Vrai si le descriptif permet de construire des venues.</summary>
    public static bool IsUsable(FormatLayout layout, EpisodeFieldNames? names = null)
    {
        names ??= EpisodeFieldNames.Default;
        return Find(layout, names.Ipp) is not null && Find(layout, names.Date) is not null;
    }

    /// <summary>Lit les venues d'un fichier. Liste vide si le descriptif ne suffit pas.</summary>
    public static IReadOnlyList<AmbulatoryVisit> Read(
        string path,
        FormatLayout layout,
        EpisodeFieldNames? names = null)
    {
        names ??= EpisodeFieldNames.Default;

        var ippField = Find(layout, names.Ipp);
        var dateField = Find(layout, names.Date);
        if (ippField is null || dateField is null) return Array.Empty<AmbulatoryVisit>();

        var umField = Find(layout, names.Um);
        var siteField = Find(layout, names.Site);
        var fileName = Path.GetFileName(path);
        var visits = new List<AmbulatoryVisit>();

        using var reader = new StreamReader(path, PmsiEncoding.Latin1);
        while (reader.ReadLine() is { } line)
        {
            if (line.Trim().Length == 0) continue;

            var ipp = ippField.Read(line);
            if (ipp.Length == 0) continue;

            var date = EpisodeBuilder.ParseDate(dateField.Read(line));
            if (date is null) continue;

            visits.Add(new AmbulatoryVisit(
                Ipp: ipp,
                Date: date.Value,
                Um: umField?.Read(line) ?? string.Empty,
                Site: siteField?.Read(line) ?? string.Empty,
                SourceFile: fileName));
        }

        return visits;
    }

    private static FormatField? Find(FormatLayout layout, IReadOnlyList<string> candidates)
    {
        foreach (var candidate in candidates)
        {
            var field = layout.Fields.FirstOrDefault(
                f => string.Equals(f.Name, candidate, StringComparison.OrdinalIgnoreCase));
            if (field is not null) return field;
        }
        return null;
    }
}
