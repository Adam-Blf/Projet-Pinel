namespace Pinel.Core.Formats;

/// <summary>
/// Canonical ATIH format matrix. Single source of truth for fixed-width
/// parsing across the ATIH formats supported by Pinel.
/// Non-ASCII chars are expressed as \u escapes to keep the file ASCII-safe
/// regardless of the source-encoding heuristic used by the compiler.
///
/// PROVENANCE, and why it matters. Until 2026-08-28 these positions came from
/// an earlier Python script and had never been checked against a descriptor.
/// They were then compared, entry by entry, against the official ATIH
/// workbooks "formats_psy_2026", "formats_mco_2026", "formats_smr_2026",
/// "formats_had_2026" and their "formats_anonymes_*" counterparts.
///
/// Outcome of that comparison:
///   - RPS and RAA, the two formats that ARE the RIM-P and the only ones this
///     DIM produces daily, were already exactly right. They are untouched.
///   - The anonymised outputs of PIVOINE (RPSA, R3A) carry an irreversible
///     hash of the IPP and NO date of birth. Reading them at the RPS offsets
///     filed that hash in the patient index under the name "IPP".
///   - Several formats carry no patient identifier at all.
///   - EDGAR was removed: it is a typology of ambulatory acts (entretien,
///     demarche, groupe, accompagnement, reunion) coded INSIDE the RAA, not a
///     file format. No national file bears that name.
///
/// A position that could not be established on an official descriptor is
/// declared with <c>CarriesPatientIdentifiers: false</c> rather than guessed.
/// </summary>
public static class AtihMatrix
{
    public static readonly IReadOnlyDictionary<string, AtihFormat> All = new Dictionary<string, AtihFormat>
    {
        // PSY natifs, produits par l'etablissement. Verifies sur "formats_psy_2026",
        // feuilles RPS et RAA : IPP en 22-41, date de naissance en 42-49.
        ["RPS"]         = new("RPS",         154,  21,  41,  41,  49, "Résumé Par Séquence — Hospitalisation PSY", "PSY", 2007),
        ["RAA"]         = new("RAA",          96,  21,  41,  41,  49, "Recueil Activité Ambulatoire — PSY", "PSY", 2007),

        // PSY anonymises, produits par PIVOINE. Le descriptif officiel
        // "formats_anonymes_psy_2026" ne declare qu'un "cryptage irreversible
        // de l'IPP" en 25-40, et AUCUNE date de naissance.
        ["RPSA"]        = new("RPSA",        157,   0,   0,   0,   0, "Résumé Par Séquence Anonyme — sortie PIVOINE, sans identifiant patient", "PSY", 2007, CarriesPatientIdentifiers: false),
        ["R3A"]         = new("R3A",         139,   0,   0,   0,   0, "Résumé Activité Ambulatoire Anonyme — sortie PIVOINE, sans identifiant patient", "PSY", 2009, CarriesPatientIdentifiers: false),

        // PSY complementaires. Aucun ne porte d'identifiant patient.
        ["FICHSUP-PSY"] = new("FICHSUP-PSY",  83,   0,   0,   0,   0, "FichSup — recueil agrégé, supprimé en psychiatrie depuis le 01/01/2021", "PSY", 2012, CarriesPatientIdentifiers: false),
        ["FICUM-PSY"]   = new("FICUM-PSY",    38,   0,   0,   0,   0, "Fichier des unités médicales — référentiel d'UM, sans donnée patient", "PSY", 2017, CarriesPatientIdentifiers: false),
        ["RSF-ACE-PSY"] = new("RSF-ACE-PSY", 246, 210, 230,   0,   0, "RSF-A début de facture — PSY ex-OQN", "PSY", 2020),

        // SSR et SMR.
        ["RHS"]          = new("RHS",          177,   0,   0,  68,  76, "Résumé Hebdomadaire Standardisé — SMR, sans identifiant patient", "SSR", 2003, CarriesPatientIdentifiers: false),
        ["SSRHA"]        = new("SSRHA",         53,   0,   0,   0,   0, "SMR anonymisé — transmission ARS, sans identifiant patient", "SSR", 2009, CarriesPatientIdentifiers: false),
        ["RAPSS"]        = new("RAPSS",        164,   0,   0,   0,   0, "RAPSS anonyme — sans identifiant patient", "SSR", 2009, CarriesPatientIdentifiers: false),
        ["FICHCOMP-SMR"] = new("FICHCOMP-SMR",  63,   0,   0,   0,   0, "FichComp transports SMR — sans identifiant patient", "SSR", 2012, CarriesPatientIdentifiers: false),

        // HAD.
        ["RPSS"]         = new("RPSS",         190,  21,  41,  61,  69, "RPSS — Résumé Par Sous-Séquence HAD", "HAD", 2005),
        ["RAPSS-HAD"]    = new("RAPSS-HAD",    164,   0,   0,   0,   0, "RAPSS-HAD anonyme — sans identifiant patient", "HAD", 2009, CarriesPatientIdentifiers: false),
        ["FICHCOMP-HAD"] = new("FICHCOMP-HAD", 105,   0,   0,   0,   0, "FichComp HAD — sans identifiant patient", "HAD", 2010, CarriesPatientIdentifiers: false),
        ["SSRHA-HAD"]    = new("SSRHA-HAD",    120,   0,   0,   0,   0, "Anonymisé HAD post-groupage — sans identifiant patient", "HAD", 2012, CarriesPatientIdentifiers: false),

        // Transversaux. VID-HOSP verifie sur les quatre champs, identique partout :
        // identifiant patient en 354-373, date de naissance en 20-27, 520 caracteres.
        ["VID-HOSP"]     = new("VID-HOSP",     520, 353, 373,  19,  27, "Vidhosp — entrée du chaînage anonyme, traitée par MAGIC", "TRANSVERSAL", 2009),
        ["ANO-HOSP"]     = new("ANO-HOSP",    1064,   0,   0,   0,   0, "ANO — sortie de MAGIC, le NIR y est remplacé par la clé de chaînage", "TRANSVERSAL", 2009, CarriesPatientIdentifiers: false),
        ["FICHCOMP"]     = new("FICHCOMP",      63,   0,   0,   0,   0, "FichComp transports — rattaché au séjour, sans identifiant patient", "TRANSVERSAL", 2010, CarriesPatientIdentifiers: false),

        // MCO.
        ["RSS"]          = new("RSS",          198,   0,   0,  62,  70, "RSS/RUM — MCO, sans identifiant patient", "MCO", 1991, CarriesPatientIdentifiers: false),
        ["RSFA"]         = new("RSFA",         257, 221, 241,   0,   0, "RSF-A début de facture — MCO", "MCO", 2009),
        ["RSFB"]         = new("RSFB",         182,   0,   0,   0,   0, "RSF-B prestations hospitalières — MCO, sans identifiant patient", "MCO", 2009, CarriesPatientIdentifiers: false),
        ["RSFC"]         = new("RSFC",         167,   0,   0,   0,   0, "RSF-C honoraires — MCO, sans identifiant patient", "MCO", 2009, CarriesPatientIdentifiers: false),
    };

    /// <summary>
    /// Alternative length variants for legacy files that did not yet match the
    /// current canonical length. Only RPS and RAA have them, and only they are
    /// verified against an official descriptor.
    /// </summary>
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
