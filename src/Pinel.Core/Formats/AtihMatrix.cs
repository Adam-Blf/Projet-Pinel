namespace Pinel.Core.Formats;

/// <summary>
/// Canonical ATIH format matrix. Single source of truth for fixed-width
/// parsing across the 23 formats supported by Pinel.
/// Port of <c>ATIH_MATRIX</c> from <c>backend/data_processor.py</c>.
/// Non-ASCII chars are expressed as \u escapes to keep the file ASCII-safe
/// regardless of the source-encoding heuristic used by the compiler.
/// </summary>
public static class AtihMatrix
{
    public static readonly IReadOnlyDictionary<string, AtihFormat> All = new Dictionary<string, AtihFormat>
    {
        // PSY natifs
        ["RPS"]         = new("RPS",         154,  21,  41,  41,  49, "R\u00e9sum\u00e9 Par S\u00e9quence \u2014 Hospitalisation PSY (P05)", "PSY", 2007),
        ["RAA"]         = new("RAA",          96,  21,  41,  41,  49, "Recueil Activit\u00e9 Ambulatoire \u2014 PSY (P06, DAF)", "PSY", 2007),
        ["RPSA"]        = new("RPSA",        154,  21,  41,  41,  49, "R\u00e9sum\u00e9 Par S\u00e9quence Anonyme \u2014 PSY (ARS)", "PSY", 2007),
        ["R3A"]         = new("R3A",          96,  21,  41,  41,  49, "R\u00e9sum\u00e9 Activit\u00e9 Ambulatoire Anonyme \u2014 PSY (ARS)", "PSY", 2009),

        // PSY complementaires
        ["FICHSUP-PSY"] = new("FICHSUP-PSY", 120,  21,  41,  41,  49, "Fichier suppl\u00e9mentaire PSY (isolement, contention, fugue)", "PSY", 2012),
        ["EDGAR"]       = new("EDGAR",        96,  21,  41,  41,  49, "EDGAR \u2014 Cotation actes ambulatoires PSY", "PSY", 2015),
        ["FICUM-PSY"]   = new("FICUM-PSY",    80,  18,  38,  38,  46, "FicUM-PSY \u2014 Unit\u00e9s m\u00e9dicales psychiatriques", "PSY", 2017),
        ["RSF-ACE-PSY"] = new("RSF-ACE-PSY", 310, 221, 241,  41,  49, "RSF-ACE PSY \u2014 Activit\u00e9 externe psychiatrique (OQN)", "PSY", 2020),

        // SSR / SMR
        ["RHS"]          = new("RHS",          192,  21,  41,  41,  49, "R\u00e9sum\u00e9 Hebdomadaire Standardis\u00e9 \u2014 SSR/SMR (S04)", "SSR", 2003),
        ["SSRHA"]        = new("SSRHA",        192,  21,  41,  41,  49, "SSR-HA \u2014 RHS Anonymis\u00e9 (transmission ARS/ATIH)", "SSR", 2009),
        ["RAPSS"]        = new("RAPSS",        140,  21,  41,  41,  49, "RAPSS \u2014 R\u00e9sum\u00e9 Anonyme Par Sous-S\u00e9quence SSR/SMR", "SSR", 2009),
        ["FICHCOMP-SMR"] = new("FICHCOMP-SMR", 105,  11,  31,  31,  39, "FichComp SMR \u2014 Donn\u00e9es compl\u00e9mentaires SSR", "SSR", 2012),

        // HAD
        ["RPSS"]         = new("RPSS",         162,  21,  41,  41,  49, "RPSS \u2014 R\u00e9sum\u00e9 Par Sous-S\u00e9quence HAD (H07)", "HAD", 2005),
        ["RAPSS-HAD"]    = new("RAPSS-HAD",    162,  21,  41,  41,  49, "RAPSS-HAD \u2014 R\u00e9sum\u00e9 Anonyme HAD (transmission ARS)", "HAD", 2009),
        ["FICHCOMP-HAD"] = new("FICHCOMP-HAD", 105,  11,  31,  31,  39, "FichComp HAD \u2014 Donn\u00e9es compl\u00e9mentaires HAD", "HAD", 2010),
        ["SSRHA-HAD"]    = new("SSRHA-HAD",    160,  21,  41,  41,  49, "SSRHA-HAD \u2014 R\u00e9sum\u00e9 anonymis\u00e9 HAD post-groupage", "HAD", 2012),

        // Transversaux
        ["VID-HOSP"]     = new("VID-HOSP",     518, 265, 285,  19,  27, "Vidhosp V015/V016 \u2014 Cha\u00eenage anonyme (depuis 2009)", "TRANSVERSAL", 2009),
        ["ANO-HOSP"]     = new("ANO-HOSP",     206,  18,  38,  38,  46, "ANO-HOSP \u2014 Anonymisation patient (couverture AMO)", "TRANSVERSAL", 2009),
        ["FICHCOMP"]     = new("FICHCOMP",     105,  11,  31,  31,  39, "FichComp \u2014 Donn\u00e9es compl\u00e9mentaires (DMI, isolement)", "TRANSVERSAL", 2010),

        // MCO
        ["RSS"]          = new("RSS",          177,  12,  32,  62,  70, "RSS/RUM \u2014 MCO (format fondateur depuis 1991)", "MCO", 1991),
        ["RSFA"]         = new("RSFA",         310, 221, 241,  41,  49, "RSF-A \u2014 Activit\u00e9 externe MCO", "MCO", 2009),
        ["RSFB"]         = new("RSFB",         350,  39,  59,  89,  97, "RSF-B \u2014 S\u00e9jour MCO", "MCO", 2009),
        ["RSFC"]         = new("RSFC",         280,  30,  50,  50,  58, "RSF-C \u2014 Honoraires MCO", "MCO", 2009),
    };

    public static readonly IReadOnlyDictionary<string, IReadOnlyList<AtihFormatVariant>> Variants =
        new Dictionary<string, IReadOnlyList<AtihFormatVariant>>
        {
            ["RPS"] = new[]
            {
                new AtihFormatVariant(142, 21, 41, 41, 49, "RPS-P04-142 (ancien 2021)"),
                new AtihFormatVariant(148, 21, 41, 41, 49, "RPS-P04-148 (transition 2021)"),
            },
            ["RAA"] = new[]
            {
                new AtihFormatVariant(86, 21, 41, 41, 49, "RAA-ancien-86 (2021)"),
                new AtihFormatVariant(90, 21, 41, 41, 49, "RAA-ancien-90 (transition 2021)"),
            },
        };

    public static AtihFormat Require(string name) =>
        All.TryGetValue(name, out var f)
            ? f
            : throw new KeyNotFoundException($"Unknown ATIH format: {name}");
}
