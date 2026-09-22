using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Pinel.Core.Anonymization;

/// <summary>
/// Transformations elementaires, toutes derivees d'un HMAC-SHA256 sous une cle
/// secrete propre au poste.
/// </summary>
/// <remarks>
/// <para>
/// Meme cle, meme valeur, meme pseudonyme : le chainage entre fichiers (IPP du
/// RPS et du VID-HOSP, numero de sejour du RPS et du HOSP-PMSI) survit a la
/// transformation. Sans la cle, rien ne permet de remonter a la valeur.
/// </para>
/// <para>
/// Chaque sortie garde la largeur et la nature (chiffres ou alphanumerique) de
/// l'entree : les controles de format de Pinel s'appliquent aux copies comme
/// aux originaux.
/// </para>
/// </remarks>
public sealed class Pseudonymizer
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private readonly byte[] _key;

    public Pseudonymizer(byte[] key)
    {
        if (key.Length < 32) throw new ArgumentException("La cle doit faire au moins 32 octets.", nameof(key));
        _key = key;
    }

    private byte[] Mac(string domain, string value) =>
        HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(domain + "\u001f" + value));

    /// <summary>
    /// Pseudonyme de meme longueur que <paramref name="value"/> : chiffres si la
    /// valeur n'est faite que de chiffres, alphanumerique sinon. Vide reste vide.
    /// </summary>
    public string Token(string domain, string value)
    {
        if (value.Length == 0) return value;
        bool digits = value.All(char.IsAsciiDigit);
        var sb = new StringBuilder(value.Length);
        int round = 0;
        while (sb.Length < value.Length)
        {
            foreach (var b in Mac(domain + round, value))
            {
                if (sb.Length == value.Length) break;
                sb.Append(digits ? (char)('0' + b % 10) : Alphabet[b % Alphabet.Length]);
            }
            round++;
        }
        return sb.ToString();
    }

    /// <summary>
    /// Decalage de dates propre a un patient, entre -28 et +28 jours, jamais nul.
    /// Tous les evenements d'un meme patient bougent ensemble : durees de sejour,
    /// sequences et continuite des episodes sont preservees.
    /// </summary>
    public int DayOffset(string patientKey)
    {
        var mac = Mac("decalage", patientKey);
        int magnitude = 1 + mac[0] % 28;
        return mac[1] % 2 == 0 ? magnitude : -magnitude;
    }

    /// <summary>Decale une date JJMMAAAA ; une valeur illisible est masquee.</summary>
    public static string ShiftDate(string value, int days)
    {
        if (value.Trim().Length == 0 || value.All(c => c == '0')) return value;
        if (DateTime.TryParseExact(value, "ddMMyyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date.AddDays(days).ToString("ddMMyyyy", CultureInfo.InvariantCulture);
        }
        return Mask(value);
    }

    /// <summary>
    /// Date de naissance : l'annee est gardee (l'age compte en psychiatrie
    /// infanto-juvenile), jour et mois sont tires de la valeur d'origine. Deux
    /// dates differentes pour un meme patient restent differentes : les
    /// conflits d'identitovigilance restent visibles sur la copie.
    /// </summary>
    public string BirthDate(string value)
    {
        if (value.Trim().Length == 0 || value.All(c => c == '0')) return value;
        if (!DateTime.TryParseExact(value, "ddMMyyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return Mask(value);
        }
        var mac = Mac("naissance", value);
        int dayOfYear = 1 + (mac[0] << 8 | mac[1]) % 365;
        var drawn = new DateTime(date.Year, 1, 1).AddDays(dayOfYear - 1);
        if (drawn.Year != date.Year) drawn = new DateTime(date.Year, 12, 31);
        return drawn.ToString("ddMMyyyy", CultureInfo.InvariantCulture);
    }

    /// <summary>Code postal : deux premiers caracteres gardes, le reste a zero.</summary>
    public static string PostalCode(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length < 2) return value;
        return (trimmed[..2] + new string('0', trimmed.Length - 2)).PadRight(value.Length);
    }

    /// <summary>Remplace chiffres et lettres par un caractere neutre, garde les espaces.</summary>
    public static string Mask(string value) =>
        new(value.Select(c => char.IsAsciiDigit(c) ? '9' : char.IsLetter(c) ? 'X' : c).ToArray());
}
