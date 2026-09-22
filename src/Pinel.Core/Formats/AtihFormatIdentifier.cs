using System.Text.RegularExpressions;

namespace Pinel.Core.Formats;

/// <summary>
/// Identifies the ATIH format of a file by its filename.
/// Port of <c>_FORMAT_RULES</c> + <c>identify_format()</c> from
/// <c>backend/data_processor.py</c>.
/// Order matters: specific formats first to avoid false positives
/// (e.g. "RPSA" before "RPS", "R3A" before "RAA").
/// </summary>
public static class AtihFormatIdentifier
{
    private static readonly (Regex Pattern, string Format)[] Rules =
    {
        // Fichiers de liaison et noms des exports Druides, releves sur les
        // lots reels 2026 du DIM. En tete : "PSY_RAA_HOSP_PMSI" est un
        // HOSP-PMSI, pas un RAA, et "vh_psy" un VID-HOSP.
        (new(@"hosp[\-_.]?pmsi", RegexOptions.IgnoreCase | RegexOptions.Compiled), "HOSP-PMSI"),
        (new(@"hosp[\-_.]?fact", RegexOptions.IgnoreCase | RegexOptions.Compiled), "HOSP-FACT"),
        (new(@"vid[\-_.]?ipp|(^|[\-_ ])vipp|(^|[\-_ ])ipp_\d", RegexOptions.IgnoreCase | RegexOptions.Compiled), "VID-IPP"),
        (new(@"(^|[\-_ ])vh[\-_.]", RegexOptions.IgnoreCase | RegexOptions.Compiled), "VID-HOSP"),
        (new(@"fc[\-_.]?ic|(^|[\-_ ])iso_\d|fichcomp[\-_ ]?iso", RegexOptions.IgnoreCase | RegexOptions.Compiled), "FICHCOMP-ISO"),
        (new(@"fc[\-_.]?htp|(^|[\-_ ])tp_\d|fichcomp[\-_ ]?(temps|tp)", RegexOptions.IgnoreCase | RegexOptions.Compiled), "FICHCOMP-TP"),

        // PSY spécifiques (haute priorité)
        (new(@"rpsa", RegexOptions.IgnoreCase | RegexOptions.Compiled),  "RPSA"),
        (new(@"r3a", RegexOptions.IgnoreCase | RegexOptions.Compiled),   "R3A"),
        (new(@"fichsup[\-_.]?psy|fichsup|fic[\-_]?sup", RegexOptions.IgnoreCase | RegexOptions.Compiled), "FICHSUP-PSY"),
        // EDGAR volontairement absent : ce n'est pas un format de fichier mais
        // une typologie d'actes ambulatoires (entretien, demarche, groupe,
        // accompagnement, reunion) codee A L'INTERIEUR du RAA. Le declarer ici
        // faisait reconnaitre comme un fichier ce qui est une valeur de champ.
        (new(@"ficum[\-_.]?psy|ficum", RegexOptions.IgnoreCase | RegexOptions.Compiled), "FICUM-PSY"),
        (new(@"rsf[\-_.]?ace[\-_.]?psy", RegexOptions.IgnoreCase | RegexOptions.Compiled), "RSF-ACE-PSY"),

        // SSR / SMR
        (new(@"fichcomp[\-_.]?smr|fichcomp[\-_.]?ssr", RegexOptions.IgnoreCase | RegexOptions.Compiled), "FICHCOMP-SMR"),
        (new(@"ssrha[\-_.]?had", RegexOptions.IgnoreCase | RegexOptions.Compiled), "SSRHA-HAD"),
        (new(@"ssrha", RegexOptions.IgnoreCase | RegexOptions.Compiled), "SSRHA"),
        (new(@"rapss[\-_.]?had", RegexOptions.IgnoreCase | RegexOptions.Compiled), "RAPSS-HAD"),
        (new(@"rapss", RegexOptions.IgnoreCase | RegexOptions.Compiled), "RAPSS"),
        (new(@"rhs", RegexOptions.IgnoreCase | RegexOptions.Compiled),   "RHS"),

        // HAD
        (new(@"fichcomp[\-_.]?had", RegexOptions.IgnoreCase | RegexOptions.Compiled), "FICHCOMP-HAD"),
        (new(@"rpss", RegexOptions.IgnoreCase | RegexOptions.Compiled),  "RPSS"),

        // Transversaux
        (new(@"vid[\-_.]?hosp|vidhosp|\.vid", RegexOptions.IgnoreCase | RegexOptions.Compiled), "VID-HOSP"),
        (new(@"ano[\-_.]?hosp|anohosp", RegexOptions.IgnoreCase | RegexOptions.Compiled), "ANO-HOSP"),

        // MCO RSF (ordre C > B > A)
        (new(@"rsf[\-_.]?c|rsfc", RegexOptions.IgnoreCase | RegexOptions.Compiled), "RSFC"),
        (new(@"rsf[\-_.]?b|rsfb", RegexOptions.IgnoreCase | RegexOptions.Compiled), "RSFB"),
        (new(@"rsf[\-_.]?a|rsfa|rsf[\-_.]?ace", RegexOptions.IgnoreCase | RegexOptions.Compiled), "RSFA"),

        // MCO + Transversal
        (new(@"fichcomp|fic[\-_]?comp|\.com", RegexOptions.IgnoreCase | RegexOptions.Compiled), "FICHCOMP"),
        (new(@"rss|rum", RegexOptions.IgnoreCase | RegexOptions.Compiled), "RSS"),

        // PSY de base (en dernier: sous-chaînes courantes)
        (new(@"rps", RegexOptions.IgnoreCase | RegexOptions.Compiled), "RPS"),
        (new(@"raa", RegexOptions.IgnoreCase | RegexOptions.Compiled), "RAA"),
    };

    /// <summary>
    /// Returns the canonical ATIH format name for <paramref name="filename"/>,
    /// or <c>null</c> if no pattern matches.
    /// </summary>
    public static string? Identify(string filename)
    {
        var baseName = Path.GetFileName(filename).ToLowerInvariant();
        foreach (var (pattern, format) in Rules)
        {
            if (pattern.IsMatch(baseName))
            {
                return format;
            }
        }
        return null;
    }
}
