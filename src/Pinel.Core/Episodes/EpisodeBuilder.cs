using System.Globalization;

namespace Pinel.Core.Episodes;

/// <summary>Convention de date attendue dans le fichier lu.</summary>
public enum DateConvention
{
    /// <summary>Convention non precisee, AAAAMMJJ essaye en premier.</summary>
    Unknown,

    /// <summary>AAAAMMJJ, convention des formats d'activite PSY.</summary>
    YearFirst,

    /// <summary>JJMMAAAA, convention des fichiers complementaires.</summary>
    DayFirst,
}

/// <summary>Une venue ambulatoire, unite de base du regroupement.</summary>
/// <param name="Ipp">Identifiant du patient.</param>
/// <param name="Date">Date de la venue, sans horodatage, comme dans le recueil.</param>
/// <param name="Um">Unite medicale.</param>
/// <param name="Site">Site geographique.</param>
/// <param name="SourceFile">Fichier d'origine, pour la tracabilite.</param>
public sealed record AmbulatoryVisit(string Ipp, DateOnly Date, string Um, string Site, string SourceFile = "");

/// <summary>
/// Regles de continuite. Le cahier des charges ne les fixe pas, elles sont
/// donc explicites et modifiables, et affichees a l'utilisateur.
/// </summary>
/// <param name="MaxGapDays">
/// Nombre de jours sans venue toleres a l'interieur d'un episode. 0 signifie
/// que deux venues doivent etre sur des journees consecutives.
/// </param>
/// <param name="SplitOnUmChange">Rompre l'episode au changement d'unite medicale.</param>
/// <param name="SplitOnSiteChange">Rompre l'episode au changement de site.</param>
/// <param name="CloseAtYearEnd">
/// Fermer tout episode au 31 decembre. Regle retenue par le DIM : le recueil
/// est annuel, un episode ne franchit donc jamais la limite d'annee.
/// </param>
public sealed record EpisodeRules(
    int MaxGapDays = 0,
    bool SplitOnUmChange = true,
    bool SplitOnSiteChange = true,
    bool CloseAtYearEnd = true)
{
    public static EpisodeRules Default { get; } = new();

    /// <summary>Description lisible, reprise telle quelle dans les exports et l'interface.</summary>
    public string Describe()
    {
        var parts = new List<string>
        {
            MaxGapDays == 0
                ? "journees strictement consecutives"
                : $"tolerance de {MaxGapDays} jour(s) sans venue",
        };
        if (SplitOnUmChange) parts.Add("rupture au changement d'UM");
        if (SplitOnSiteChange) parts.Add("rupture au changement de site");
        if (CloseAtYearEnd) parts.Add("fermeture au 31 decembre");
        return string.Join(", ", parts);
    }
}

/// <summary>Un episode de prise en charge ambulatoire.</summary>
public sealed record Episode(
    string EpisodeId,
    string Ipp,
    string Um,
    string Site,
    DateOnly Start,
    DateOnly End,
    int VisitCount)
{
    /// <summary>
    /// Duree de presence en jours. Une venue isolee vaut 0, deux journees
    /// consecutives valent 1. Au-dela de 1 en secteur d'urgence, le DIM
    /// considere qu'il y a un probleme d'aval ou d'organisation.
    /// </summary>
    public int DurationDays => End.DayNumber - Start.DayNumber;
}

/// <summary>
/// Construit les episodes de prise en charge ambulatoire, deuxieme chantier du
/// cahier des charges.
/// </summary>
/// <remarks>
/// Dans le format national, l'activite ambulatoire de psychiatrie n'est
/// rattachee ni a un sejour ni a un dossier. Le DIM a besoin d'une maille
/// intermediaire pour mesurer, par exemple, une duree de presence au SAU. Cet
/// assembleur regroupe les journees consecutives d'un meme patient dans une
/// meme UM et un meme site, et attribue a chaque groupe un identifiant stable.
/// </remarks>
public static class EpisodeBuilder
{
    /// <summary>
    /// Regroupe les venues en episodes. Les venues en double sur une meme
    /// journee, une meme UM et un meme site comptent pour une seule.
    /// </summary>
    public static IReadOnlyList<Episode> Build(IEnumerable<AmbulatoryVisit> visits, EpisodeRules? rules = null)
    {
        rules ??= EpisodeRules.Default;
        var episodes = new List<Episode>();

        var groups = visits
            .Where(v => !string.IsNullOrWhiteSpace(v.Ipp))
            .GroupBy(v => (
                v.Ipp,
                Um: rules.SplitOnUmChange ? v.Um : string.Empty,
                Site: rules.SplitOnSiteChange ? v.Site : string.Empty));

        foreach (var group in groups)
        {
            var dates = group
                .Select(v => v.Date)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            if (dates.Count == 0) continue;

            var start = dates[0];
            var previous = dates[0];
            var count = 1;

            for (int i = 1; i < dates.Count; i++)
            {
                var current = dates[i];
                var gap = current.DayNumber - previous.DayNumber - 1;
                var crossesYear = rules.CloseAtYearEnd && current.Year != previous.Year;

                if (gap > rules.MaxGapDays || crossesYear)
                {
                    episodes.Add(Create(group.Key.Ipp, group.Key.Um, group.Key.Site, start, previous, count));
                    start = current;
                    count = 0;
                }

                previous = current;
                count++;
            }

            episodes.Add(Create(group.Key.Ipp, group.Key.Um, group.Key.Site, start, previous, count));
        }

        return episodes
            .OrderBy(e => e.Ipp, StringComparer.Ordinal)
            .ThenBy(e => e.Start)
            .ToList();
    }

    private static Episode Create(string ipp, string um, string site, DateOnly start, DateOnly end, int visits)
    {
        // Identifiant stable et lisible : patient, site, UM, date de debut.
        // Deux executions sur le meme lot produisent les memes identifiants.
        var id = string.Join('-', new[]
        {
            Sanitize(ipp),
            Sanitize(string.IsNullOrWhiteSpace(site) ? "NA" : site),
            Sanitize(string.IsNullOrWhiteSpace(um) ? "NA" : um),
            start.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
        });

        return new Episode(id, ipp, um, site, start, end, visits);
    }

    /// <summary>
    /// Normalise un segment d'identifiant. Les caracteres non alphanumeriques
    /// sont remplaces, jamais supprimes : sans cela, les codes UM-01 et UM01,
    /// pourtant distincts, produiraient le meme identifiant d'episode et
    /// casseraient toute jointure en aval.
    /// </summary>
    private static string Sanitize(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0) return "NA";

        var builder = new System.Text.StringBuilder(trimmed.Length);
        foreach (var character in trimmed)
        {
            builder.Append(char.IsLetterOrDigit(character) ? char.ToUpperInvariant(character) : '_');
        }
        return builder.ToString();
    }

    /// <summary>Annee la plus ancienne acceptee dans un recueil PMSI.</summary>
    public const int MinYear = 1990;

    /// <summary>Annee la plus lointaine acceptee, marge au-dela de l'exercice courant.</summary>
    public const int MaxYear = 2100;

    /// <summary>
    /// Lit une date de recueil. Les formats d'activite PSY utilisent AAAAMMJJ,
    /// les fichiers complementaires JJMMAAAA. La convention attendue est passee
    /// explicitement ; a defaut, AAAAMMJJ est essaye en premier, puis JJMMAAAA.
    /// </summary>
    /// <remarks>
    /// Une date dont l'annee sort de la plage <see cref="MinYear"/> a
    /// <see cref="MaxYear"/> est rejetee, meme si elle s'analyse
    /// techniquement : 01021201 se lit comme l'an 102 en AAAAMMJJ, ce qui est
    /// absurde, et vaut le 1er fevrier 1201 en JJMMAAAA, ce qui l'est aussi.
    /// Mieux vaut ecarter la ligne que fabriquer un episode faux.
    /// </remarks>
    public static DateOnly? ParseDate(string value, DateConvention convention = DateConvention.Unknown)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length != 8) return null;

        var order = convention switch
        {
            DateConvention.DayFirst => new[] { "ddMMyyyy", "yyyyMMdd" },
            _ => new[] { "yyyyMMdd", "ddMMyyyy" },
        };

        foreach (var format in order)
        {
            if (DateOnly.TryParseExact(digits, format, CultureInfo.InvariantCulture,
                                       DateTimeStyles.None, out var parsed)
                && parsed.Year >= MinYear && parsed.Year <= MaxYear)
            {
                return parsed;
            }
        }

        return null;
    }
}
