namespace Pinel.Core.Checks;

/// <summary>
/// Builds the text of a <see cref="CheckFinding"/> so that it LOCATES a defect
/// without CARRYING the offending value.
/// <para>
/// Why this exists: a finding is rendered in the UI and serialised into the JSON
/// pre-flight report written on disk. Any patient identifier (NIR, IPP, date of
/// birth) copied into <see cref="CheckFinding.Message"/> or
/// <see cref="CheckFinding.FixHint"/> therefore leaves the DIM workstation in
/// clear text, which is a GDPR incident and not a style defect.
/// </para>
/// <para>
/// Rule applied by every validator of this namespace:
/// <list type="number">
///   <item>locate with the line number already carried by the finding, plus
///     <see cref="Position"/> for the column and the length of the field;</item>
///   <item>describe the GAP - expected versus read - with
///     <see cref="DigitGap"/> or <see cref="CharacterGap"/>, never the value;</item>
///   <item>never echo an exception raised by the file system: its message
///     carries an absolute path. Use <see cref="ReadFailure"/>.</item>
/// </list>
/// A partial mask is deliberately absent: every finding produced here is tied to
/// a real line number, which already discriminates two records of the same file.
/// </para>
/// </summary>
internal static class FindingRedaction
{
    /// <summary>Generic wording for a read failure other than a denied access.</summary>
    public const string ReadFailedMessage =
        "Lecture du fichier impossible : fichier verrouillé, absent ou illisible.";

    /// <summary>Generic wording for a read failure caused by file-system rights.</summary>
    public const string ReadDeniedMessage =
        "Lecture du fichier impossible : accès refusé par le système de fichiers.";

    /// <summary>Actionable tip shared by every <c>ERR-IO</c> finding.</summary>
    public const string ReadFailureHint =
        "Vérifier les droits d'accès, la présence du fichier et qu'il n'est pas ouvert ailleurs.";

    /// <summary>
    /// Classifies a read failure without echoing <c>error.Message</c>, which
    /// contains the absolute path of the file on every platform.
    /// </summary>
    public static string ReadFailure(Exception error) =>
        error is UnauthorizedAccessException ? ReadDeniedMessage : ReadFailedMessage;

    /// <summary>
    /// Locates a fixed-width field for a human reader: 1-indexed column and
    /// declared length, e.g. <c>"colonne 266, longueur 20"</c>.
    /// </summary>
    /// <param name="startIndex">Zero-indexed start of the field in the record.</param>
    /// <param name="length">Declared length of the field.</param>
    public static string Position(int startIndex, int length) =>
        $"colonne {startIndex + 1}, longueur {length}";

    /// <summary>
    /// Describes the gap between an expected digit count and what was actually
    /// read, e.g. <c>"13 chiffres attendus, 11 caractères lus dont 2 non numériques"</c>.
    /// Counts characters, never reproduces them.
    /// </summary>
    public static string DigitGap(int expectedDigits, ReadOnlySpan<char> value)
    {
        int nonDigits = 0;
        foreach (var c in value)
        {
            if (!char.IsDigit(c)) nonDigits++;
        }

        var gap = $"{expectedDigits} chiffres attendus, {value.Length} {Plural(value.Length, "caractère")} lus";
        return nonDigits == 0
            ? gap
            : $"{gap} dont {nonDigits} non {Plural(nonDigits, "numérique")}";
    }

    /// <summary>
    /// Describes how many characters of a given class were found, e.g.
    /// <c>"3 lettres sur 20 caractères lus"</c>. Counts characters, never
    /// reproduces them.
    /// </summary>
    public static string CharacterGap(int count, string singularLabel, int total) =>
        $"{count} {Plural(count, singularLabel)} sur {total} {Plural(total, "caractère")} lus";

    private static string Plural(int count, string singular) =>
        count > 1 ? singular + "s" : singular;
}
