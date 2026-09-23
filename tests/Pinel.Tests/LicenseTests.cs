using System.Text;
using Pinel.Core.Licensing;

namespace Pinel.Tests;

/// <summary>
/// Licence d'un etablissement : verifiee hors ligne, rattachee a son FINESS,
/// avec une tolerance apres l'echeance pour ne pas bloquer un DIM en pleine
/// periode de transmission.
/// </summary>
/// <remarks>
/// Les licences de ces tests sont emises par tools/licence.py avec la cle de
/// developpement. Elles ne valent que pour un FINESS fictif.
/// </remarks>
public sealed class LicenseTests
{
    // Licence de test : CH de Bourgogne, FINESS 210000123, emise le 23/09/2026.
    private const string ValidLicense =
        "eyJlbWlzZSI6IjIwMjYtMDktMjMiLCJldGFibGlzc2VtZW50IjoiQ0ggZGUgQm91cmdvZ25lIiwiZXhwaXJlIjoiMjAyNy0wOS0zMCIsImZpbmVzcyI6IjIxMDAwMDEyMyIsIm51bWVybyI6IjIxMDAwMDEyMy0yMDI2MDkifQ";

    private static string Licence() => File.ReadAllText(LicensePath());

    private static string LicensePath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".secrets", "pinel-licences", "emises", "210000123-202609.lic");

    private static bool LicenseAvailable => File.Exists(LicensePath());

    [SkippableFact]
    public void Accepte_une_licence_signee_du_bon_finess()
    {
        Skip.IfNot(LicenseAvailable, "Licence de developpement absente de ce poste.");

        var content = LicenseVerifier.Read(Licence());

        Assert.Equal("210000123", content.Finess);
        Assert.Equal("CH de Bourgogne", content.Etablissement);
        Assert.StartsWith(ValidLicense[..40], Licence()[..40], StringComparison.Ordinal);
    }

    [SkippableFact]
    public void Refuse_une_licence_retouchee()
    {
        Skip.IfNot(LicenseAvailable, "Licence de developpement absente de ce poste.");

        // Un caractere change dans le contenu : la signature ne tient plus.
        var parts = Licence().Trim().Split('.');
        var trafiquee = parts[0][..^2] + (parts[0][^2] == 'A' ? 'B' : 'A') + parts[0][^1] + "." + parts[1];

        Assert.ThrowsAny<Exception>(() => LicenseVerifier.Read(trafiquee));
    }

    [Fact]
    public void Refuse_une_licence_absente()
    {
        var status = LicenseVerifier.Check("210000123", new DateOnly(2026, 9, 23));

        // Sur un poste sans licence installee, l'etat le dit clairement.
        if (!File.Exists(LicenseVerifier.LicensePath))
        {
            Assert.False(status.Valid);
            Assert.Contains("Aucune licence", status.Reason, StringComparison.OrdinalIgnoreCase);
        }
    }

    [SkippableFact]
    public void Refuse_la_licence_d_un_autre_etablissement()
    {
        Skip.IfNot(LicenseAvailable, "Licence de developpement absente de ce poste.");
        using var poste = new TempLicense(Licence());

        var status = LicenseVerifier.Check("999999999", new DateOnly(2026, 9, 23));

        Assert.False(status.Valid);
        Assert.Contains("FINESS", status.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public void Tolere_trente_jours_apres_l_echeance_puis_refuse()
    {
        Skip.IfNot(LicenseAvailable, "Licence de developpement absente de ce poste.");
        using var poste = new TempLicense(Licence());
        var expiry = DateOnly.Parse(LicenseVerifier.Read(Licence()).Expire);

        var veille = LicenseVerifier.Check("210000123", expiry.AddDays(-1));
        var pendantTolerance = LicenseVerifier.Check("210000123", expiry.AddDays(10));
        var apresTolerance = LicenseVerifier.Check("210000123", expiry.AddDays(LicenseVerifier.GraceDays + 1));

        Assert.True(veille.Valid);
        Assert.False(veille.InGracePeriod);
        Assert.True(pendantTolerance.Valid);
        Assert.True(pendantTolerance.InGracePeriod);
        Assert.False(apresTolerance.Valid);
        Assert.Contains("expirée", apresTolerance.Reason, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Pose une licence sur le poste le temps d'un test, et rend l'etat anterieur.</summary>
    private sealed class TempLicense : IDisposable
    {
        private readonly string? _previous;

        public TempLicense(string licence)
        {
            _previous = File.Exists(LicenseVerifier.LicensePath)
                ? File.ReadAllText(LicenseVerifier.LicensePath)
                : null;
            File.WriteAllText(LicenseVerifier.LicensePath, licence, Encoding.ASCII);
        }

        public void Dispose()
        {
            if (_previous is null) File.Delete(LicenseVerifier.LicensePath);
            else File.WriteAllText(LicenseVerifier.LicensePath, _previous, Encoding.ASCII);
        }
    }
}
