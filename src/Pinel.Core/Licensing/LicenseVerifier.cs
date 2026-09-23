using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pinel.Core.Security;

namespace Pinel.Core.Licensing;

/// <summary>Ce que porte une licence. Aucune donnee personnelle, aucune donnee de sante.</summary>
public sealed record LicenseContent(
    [property: JsonPropertyName("etablissement")] string Etablissement,
    [property: JsonPropertyName("finess")] string Finess,
    [property: JsonPropertyName("emise")] string Emise,
    [property: JsonPropertyName("expire")] string Expire,
    [property: JsonPropertyName("numero")] string Numero);

/// <summary>Etat de la licence sur ce poste, tel que l'application l'affiche et s'en sert.</summary>
public sealed record LicenseStatus(
    bool Valid,
    string Reason,
    LicenseContent? Content = null,
    int DaysLeft = 0,
    bool InGracePeriod = false);

/// <summary>
/// Verifie la licence d'un etablissement, hors ligne.
/// </summary>
/// <remarks>
/// <para>
/// La licence est un fichier signe : un contenu lisible et sa signature, en
/// base64, separes par un point. L'application ne porte que la cle publique,
/// elle ne peut donc pas en fabriquer. Rien n'est appele sur le reseau : les
/// postes d'un departement d'information medicale n'ont pas internet, et une
/// verification en ligne les bloquerait tous le jour ou le service tombe.
/// </para>
/// <para>
/// Une licence est rattachee au FINESS d'inscription e-PMSI de l'etablissement.
/// Recopiee ailleurs, elle ne correspond plus au FINESS des reglages et se
/// signale d'elle-meme.
/// </para>
/// <para>
/// Apres expiration, <see cref="GraceDays"/> jours de tolerance : un DIM ne
/// doit pas se retrouver bloque en pleine periode de transmission parce qu'un
/// bon de commande a pris du retard.
/// </para>
/// </remarks>
public static class LicenseVerifier
{
    public const int GraceDays = 30;

    /// <summary>Emplacement de la licence sur le poste.</summary>
    public static string LicensePath => PinelPaths.In("licence.lic");

    private static readonly string PublicKeyPem = LoadEmbeddedKey();

    /// <summary>Etat de la licence installee, rapporte au FINESS de l'etablissement.</summary>
    public static LicenseStatus Check(string? finessOfWorkstation, DateOnly? today = null)
    {
        var day = today ?? DateOnly.FromDateTime(DateTime.Today);
        if (!File.Exists(LicensePath))
        {
            return new LicenseStatus(false, "Aucune licence installée sur ce poste.");
        }

        LicenseContent content;
        try
        {
            content = Read(File.ReadAllText(LicensePath));
        }
        catch (Exception ex) when (ex is FormatException or JsonException or IOException or CryptographicException)
        {
            return new LicenseStatus(false, "Licence illisible ou signature invalide.");
        }

        if (!string.IsNullOrWhiteSpace(finessOfWorkstation)
            && !string.Equals(content.Finess, finessOfWorkstation.Trim(), StringComparison.Ordinal))
        {
            return new LicenseStatus(false,
                $"Licence émise pour le FINESS {content.Finess}, différent de celui de ce poste.", content);
        }

        if (!DateOnly.TryParse(content.Expire, out var expiry))
        {
            return new LicenseStatus(false, "Date d'expiration illisible.", content);
        }

        int daysLeft = expiry.DayNumber - day.DayNumber;
        if (daysLeft >= 0)
        {
            return new LicenseStatus(true, $"Licence valable jusqu'au {expiry:dd/MM/yyyy}.", content, daysLeft);
        }
        if (-daysLeft <= GraceDays)
        {
            return new LicenseStatus(true,
                $"Licence expirée le {expiry:dd/MM/yyyy}. Tolérance de {GraceDays} jours, il en reste {GraceDays + daysLeft}.",
                content, daysLeft, InGracePeriod: true);
        }
        return new LicenseStatus(false, $"Licence expirée le {expiry:dd/MM/yyyy}.", content, daysLeft);
    }

    /// <summary>
    /// Installe une licence recue. Le fichier n'est copie que si sa signature
    /// est valide : une licence retouchee n'entre jamais sur le poste.
    /// </summary>
    public static LicenseStatus Install(string sourceFile, string? finessOfWorkstation)
    {
        string text;
        try
        {
            text = File.ReadAllText(sourceFile);
            Read(text);
        }
        catch (Exception ex) when (ex is FormatException or JsonException or IOException or CryptographicException)
        {
            return new LicenseStatus(false, "Ce fichier n'est pas une licence Pinel valide.");
        }

        File.WriteAllText(LicensePath, text, Encoding.ASCII);
        return Check(finessOfWorkstation);
    }

    /// <summary>Lit une licence et verifie sa signature. Leve si elle ne tient pas.</summary>
    internal static LicenseContent Read(string licence)
    {
        var parts = licence.Trim().Split('.');
        if (parts.Length != 2) throw new FormatException("Licence mal formée.");

        var body = FromBase64Url(parts[0]);
        var signature = FromBase64Url(parts[1]);

        using var rsa = RSA.Create();
        rsa.ImportFromPem(PublicKeyPem);
        if (!rsa.VerifyData(body, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
        {
            throw new CryptographicException("Signature invalide.");
        }

        return JsonSerializer.Deserialize<LicenseContent>(body)
               ?? throw new JsonException("Contenu de licence vide.");
    }

    private static byte[] FromBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '='));
    }

    private static string LoadEmbeddedKey()
    {
        using var stream = typeof(LicenseVerifier).Assembly
                               .GetManifestResourceStream("Pinel.Core.Licensing.cle-publique.pem")
                           ?? throw new InvalidOperationException(
                               "Cle publique de licence absente de l'assembly.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
