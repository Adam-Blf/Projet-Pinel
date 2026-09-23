using System.Globalization;
using Pinel.Core.Anonymization;
using Pinel.Core.Formats;

namespace Pinel.Tests;

/// <summary>
/// Pseudonymisation des fichiers PMSI : ce qui doit disparaitre disparait, ce
/// qui sert aux controles et a l'apprentissage reste, et une fuite se voit.
/// Toutes les lignes sont synthetiques.
/// </summary>
public sealed class AnonymizationTests : IDisposable
{
    private static readonly byte[] Key = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pinel-anon-" + Guid.NewGuid().ToString("N"));

    public AnonymizationTests() => Directory.CreateDirectory(Path.Combine(_root, "in", "PSY"));

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    /// <summary>Descriptif RAA 2026 reduit aux champs utiles au test, positions officielles.</summary>
    private static FormatLayout Raa() => new("RAA", 2026, new[]
    {
        new FormatField("FINESS", 1, 9, "Numéro FINESS d’inscription ePMSI"),
        new FormatField("FORMAT", 19, 3, "N° de format"),
        new FormatField("IPP", 22, 20, "N° d’identification permanent du patient (IPP)"),
        new FormatField("DDN", 42, 8, "Date de naissance du patient"),
        new FormatField("SEXE", 50, 1, "Sexe du patient"),
        new FormatField("CP", 51, 5, "Code postal de résidence"),
        new FormatField("UM", 60, 4, "N° d’unité médicale"),
        new FormatField("DATE_ACTE", 70, 8, "Date de l’acte"),
        new FormatField("DP", 87, 8, "Diagnostic principal ou motif de recours"),
        new FormatField("NDA", 95, 2, "Nombre de diagnostics et facteurs associés (nDA)"),
    });

    /// <summary>Ligne RAA de 96 + 8 x nDA caracteres.</summary>
    private static string RaaLine(string ipp, string ddn, string dateActe, string dp, params string[] das)
    {
        var line = new char[96];
        Array.Fill(line, ' ');
        void Put(int start, string value) => value.CopyTo(0, line, start - 1, value.Length);
        Put(1, "940140049");
        Put(19, "P16");
        Put(22, ipp);
        Put(42, ddn);
        Put(50, "1");
        Put(51, "94550");
        Put(60, "U123");
        Put(70, dateActe);
        Put(87, dp);
        Put(95, das.Length.ToString("00", CultureInfo.InvariantCulture));
        return new string(line) + string.Concat(das.Select(d => d.PadRight(8)));
    }

    private (string Output, AnonymizationReport Report) Run(params string[] lines)
    {
        File.WriteAllLines(Path.Combine(_root, "in", "PSY", "vvd_raa.txt"), lines, PmsiEncoding.Latin1);
        var anonymizer = new PmsiAnonymizer(new Pseudonymizer(Key), new[] { RecordSpec.For(Raa()) });
        var report = anonymizer.Run(Path.Combine(_root, "in"), Path.Combine(_root, "out"));
        var output = File.ReadAllText(Path.Combine(_root, "out", "PSY", "vvd_raa.txt"), PmsiEncoding.Latin1);
        return (output, report);
    }

    [Fact]
    public void Remplace_ipp_et_date_de_naissance_et_garde_les_codes()
    {
        var (output, report) = Run(RaaLine("00123456789", "15031985", "12012026", "F200", "Z590", "F101"));
        var line = output.TrimEnd('\r', '\n');

        Assert.DoesNotContain("00123456789", output);
        Assert.DoesNotContain("15031985", output);
        Assert.Equal(96 + 16, line.Length);
        Assert.Equal("940140049", line[..9]);
        Assert.Equal("U123", line.Substring(59, 4));
        Assert.Equal("F200", line.Substring(86, 4));
        Assert.Equal("Z590    F101    ", line[96..]);
        Assert.Equal("1985", line.Substring(45, 4));         // annee de naissance gardee
        Assert.Equal("94000", line.Substring(50, 5));        // departement garde
        Assert.Equal(11, line.Substring(21, 20).Trim().Length); // pseudonyme de meme longueur
        Assert.Single(report.Written);
    }

    [Fact]
    public void Meme_patient_meme_pseudonyme_et_meme_decalage_de_dates()
    {
        var (output, _) = Run(
            RaaLine("00123456789", "15031985", "12012026", "F200"),
            RaaLine("00123456789", "15031985", "19012026", "F200"),
            RaaLine("00999999991", "01011990", "12012026", "F200"));
        var lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(lines[0].Substring(21, 20), lines[1].Substring(21, 20));
        Assert.NotEqual(lines[0].Substring(21, 20), lines[2].Substring(21, 20));

        DateTime Read(string l) => DateTime.ParseExact(l.Substring(69, 8), "ddMMyyyy", CultureInfo.InvariantCulture);
        Assert.Equal(7, (Read(lines[1]) - Read(lines[0])).Days); // intervalle preserve
        Assert.NotEqual("12012026", lines[0].Substring(69, 8));  // date deplacee
    }

    [Fact]
    public void Masque_ce_que_le_descriptif_ne_couvre_pas()
    {
        // Filler rempli (position 57) et fin de ligne en trop : caracteres inconnus.
        var line = RaaLine("00123456789", "15031985", "12012026", "F200").ToCharArray();
        "AB12".CopyTo(0, line, 56, 3);
        var normal = RaaLine("00999999991", "01011990", "12012026", "F200");
        var (output, _) = Run(normal, normal, normal, normal, new string(line) + "DUPONT JEAN 1985");

        Assert.DoesNotContain("DUPONT", output);
        Assert.DoesNotContain("JEAN", output);
        Assert.Contains("XXXXXX XXXX 9999", output);
    }

    [Fact]
    public void Signale_un_identifiant_range_dans_un_champ_de_code()
    {
        // L'IPP d'un premier patient recopie dans le champ UM d'un second.
        var (output, report) = Run(
            RaaLine("12345678", "15031985", "12012026", "F200"),
            RaaLine("00999999991", "01011990", "12012026", "F200").Remove(59, 4).Insert(59, "1234"));

        Assert.Empty(report.LeaksByField); // 4 caracteres : sous le seuil, pas un identifiant

        var (output2, report2) = Run(
            RaaLine("12345678", "15031985", "12012026", "F200"),
            RaaLine("00999999991", "01011990", "12012026", "12345678"));

        Assert.Equal(1, report2.LeaksByField["RAA.DP"]);
        Assert.DoesNotContain("12345678", output2);
    }

    [Fact]
    public void Ecarte_un_fichier_non_reconnu()
    {
        File.WriteAllLines(Path.Combine(_root, "in", "PSY", "liste_patients.txt"), new[] { "DUPONT;JEAN;1985", "MARTIN;ANNE;1990" });
        var (_, report) = Run(RaaLine("00123456789", "15031985", "12012026", "F200"));

        Assert.Contains(report.Excluded, f => f.RelativePath.EndsWith("liste_patients.txt"));
        Assert.False(File.Exists(Path.Combine(_root, "out", "PSY", "liste_patients.txt")));
    }

    [Theory]
    [InlineData("N° d’identification permanent du patient (IPP)", FieldRole.PatientId)]
    [InlineData("N° immatriculation assuré", FieldRole.Nir)]
    [InlineData("Clé du N° immatriculation", FieldRole.NirKey)]
    [InlineData("Identifiant national de santé (INS)", FieldRole.Nir)]
    [InlineData("N° de séjour", FieldRole.StayId)]
    [InlineData("N° d’entrée", FieldRole.StayId)]
    [InlineData("N° de RSS", FieldRole.StayId)]
    [InlineData("Montant total du séjour facturé au patient", FieldRole.Keep)]
    [InlineData("Mode d’entrée de séjour", FieldRole.Keep)]
    [InlineData("Date de naissance du patient", FieldRole.BirthDate)]
    [InlineData("Date de début de séquence", FieldRole.Date)]
    [InlineData("Nom du médecin traitant", FieldRole.Erase)]
    [InlineData("Nombre de diagnostics et facteurs associés (nDA)", FieldRole.Keep)]
    [InlineData("Numéro accident du travail ou date d’accident de droit commun", FieldRole.Erase)]
    [InlineData("Diagnostic principal ou motif de recours", FieldRole.Keep)]
    public void Classe_les_champs_d_apres_leur_libelle_officiel(string label, FieldRole expected)
    {
        Assert.Equal(expected, FieldClassifier.Classify(new FormatField("X", 1, 1, label)));
    }
}
