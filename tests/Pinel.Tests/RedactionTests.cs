using System.Text;
using Pinel.Core.Checks;
using Pinel.Core.Formats;
using Xunit;

namespace Pinel.Tests;

/// <summary>
/// Guards the GDPR rule of the validation pipeline. A <see cref="CheckFinding"/>
/// travels to the UI AND is serialised into the JSON pre-flight report written on
/// disk, so its <c>Message</c> must LOCATE the defect (line number, column,
/// length, gap) without CARRYING the patient identifier, and must never echo a
/// file-system exception message, which contains an absolute path.
/// The audit log follows the same rule, guarded by <c>RedactionAuditTests</c>.
/// </summary>
public sealed class RedactionTests : IDisposable
{
    // Seeded identifiers. None of them may ever surface in a Message or a FixHint.
    private const string LeakNir = "286067511004X";        // 13 chars, one non-digit
    private const string LeakIpp = "IPP-FUITE-7788990011"; // 20 chars, letters and dash
    private const string LeakOrphanIpp = "IPP-ORPHELIN-9900112";
    private const string LeakDashIpp = "7788-990011-4422";
    private const string LeakDdn = "19901225";             // AAAAMMJJ au lieu de JJMMAAAA
    private const string LeakFiness = "9400001X2";         // 9 chars, one non-digit

    private readonly string _tempDir;

    public RedactionTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _tempDir = Path.Combine(Path.GetTempPath(), "pinel_redaction_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>
    /// Builds a fixed-width record of <paramref name="length"/> spaces with each
    /// value stamped at its zero-indexed offset.
    /// </summary>
    private static string Line(int length, params (int Start, string Value)[] fields)
    {
        var sb = new StringBuilder(new string(' ', length));
        foreach (var (start, value) in fields)
        {
            for (int i = 0; i < value.Length && start + i < length; i++)
            {
                sb[start + i] = value[i];
            }
        }
        return sb.ToString();
    }

    private string Write(string name, params string[] lines)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllLines(path, lines, Encoding.Latin1);
        return path;
    }

    // ------------------------------------------------------- per-check guards

    [Fact]
    public void Nir_finding_locates_without_carrying_the_nir()
    {
        var fmt = AtihMatrix.Require("VID-HOSP");
        var file = Write("FV94_VID-HOSP_2024.txt", Line(fmt.Length, (0, LeakNir + "42")));

        var findings = new ChainageNirCheck().Validate(file, "VID-HOSP").ToList();
        var finding = Assert.Single(findings, f => f.Code == "ERR-VID-NIR-FORMAT");

        Assert.DoesNotContain(LeakNir, finding.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("286067511004", finding.Message, StringComparison.Ordinal);
        Assert.Contains("colonne 1", finding.Message, StringComparison.Ordinal);
        Assert.Equal(1, finding.LineNumber);
    }

    [Fact]
    public void Nir_key_finding_locates_without_carrying_the_key()
    {
        var fmt = AtihMatrix.Require("VID-HOSP");
        var file = Write("FV94_VID-HOSP_2024.txt", Line(fmt.Length, (0, "2860675110042K7")));

        var findings = new ChainageNirCheck().Validate(file, "VID-HOSP").ToList();
        var finding = Assert.Single(findings, f => f.Code == "ERR-VID-NIR-KEY");

        Assert.DoesNotContain("K7", finding.Message, StringComparison.Ordinal);
        Assert.Contains("colonne 14", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Anonymization_letters_finding_locates_without_carrying_the_ipp()
    {
        // RPSA ne declare plus de position d'IPP : le controle observe desormais la
        // fenetre officielle du cryptage irreversible, positions 25 a 40.
        var fmt = AtihMatrix.Require("RPSA");
        var file = Write("FV94_RPSA_2024.txt", Line(fmt.Length, (24, LeakIpp)));

        var findings = new AnonymizationCheck().Validate(file, "RPSA").ToList();
        var finding = Assert.Single(findings, f => f.Code == "ERR-ANONYM-LETTERS");

        Assert.DoesNotContain(LeakIpp, finding.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("7788990011", finding.Message, StringComparison.Ordinal);
        Assert.Contains("colonne 25", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Anonymization_dash_finding_locates_without_carrying_the_ipp()
    {
        // Meme recalage que le test precedent : la fenetre observee est celle du
        // cryptage irreversible, positions 25 a 40 du descriptif officiel.
        var fmt = AtihMatrix.Require("RPSA");
        var file = Write("FV94_RPSA_2024.txt", Line(fmt.Length, (24, LeakDashIpp)));

        var findings = new AnonymizationCheck().Validate(file, "RPSA").ToList();
        var finding = Assert.Single(findings, f => f.Code == "WARN-ANONYM-DASH");

        Assert.DoesNotContain(LeakDashIpp, finding.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("990011", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Birthdate_finding_locates_without_carrying_the_date()
    {
        var fmt = AtihMatrix.Require("RPS");
        var file = Write("FV94_RPS_2024.txt", Line(fmt.Length, (fmt.DdnStart, LeakDdn)));

        var findings = new BirthDateFormatCheck().Validate(file, "RPS").ToList();
        var finding = Assert.Single(findings, f => f.Code == "WARN-DDN-AAAAMMJJ");

        Assert.DoesNotContain(LeakDdn, finding.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("1990", finding.Message, StringComparison.Ordinal);
        Assert.Contains("colonne 42", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Finess_finding_locates_without_carrying_the_finess()
    {
        var fmt = AtihMatrix.Require("RPSA");
        var file = Write("FV94_RPSA_2024.txt", Line(fmt.Length, (0, LeakFiness)));

        var findings = new FinessCheck().Validate(file, "RPSA").ToList();
        var finding = Assert.Single(findings, f => f.Code == "ERR-FINESS-FORMAT");

        Assert.DoesNotContain(LeakFiness, finding.Message, StringComparison.Ordinal);
        Assert.Contains("colonne 1", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Chainage_findings_locate_on_the_real_line_without_carrying_the_ipp()
    {
        var rpsFmt = AtihMatrix.Require("RPS");
        var vidFmt = AtihMatrix.Require("VID-HOSP");

        // Line 1 carries no IPP: the reported line number must still be 2.
        var rps = Write("FV94_RPS_2024.txt",
            Line(rpsFmt.Length),
            Line(rpsFmt.Length, (rpsFmt.IppStart, LeakIpp)));

        var vid = Write("FV94_VID-HOSP_2024.txt",
            Line(vidFmt.Length, (0, "199046234567891"), (vidFmt.IppStart, LeakOrphanIpp)));

        var findings = new ChainageCoverageCheck().Validate(new[]
        {
            (Path: rps, Format: "RPS"),
            (Path: vid, Format: "VID-HOSP"),
        }).ToList();

        var missing = Assert.Single(findings, f => f.Code == "ERR-CHAINAGE-MANQUANT");
        Assert.DoesNotContain(LeakIpp, missing.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("7788990011", missing.Message, StringComparison.Ordinal);
        Assert.Equal(2, missing.LineNumber);

        var orphan = Assert.Single(findings, f => f.Code == "WARN-CHAINAGE-ORPHAN");
        Assert.DoesNotContain(LeakOrphanIpp, orphan.Message, StringComparison.Ordinal);
        Assert.Equal(1, orphan.LineNumber);
    }

    [Fact]
    public void Read_failure_findings_never_carry_the_absolute_path()
    {
        var fmt = AtihMatrix.Require("VID-HOSP");
        var file = Write("FV94_VID-HOSP_2024.txt", Line(fmt.Length, (0, "199046234567891")));

        using var exclusive = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None);

        IFileCheck[] validators =
        {
            new ChainageNirCheck(),
            new BirthDateFormatCheck(),
            new DuplicateLineCheck(),
            new FileYearCheck(),
            new AnonymizationCheck(),
            new FinessCheck(),
        };

        foreach (var validator in validators)
        {
            var findings = validator.Validate(file, "VID-HOSP").ToList();
            var finding = Assert.Single(findings, f => f.Code == "ERR-IO");
            Assert.DoesNotContain(_tempDir, finding.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("VID-HOSP_2024.txt", finding.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ------------------------------------------------------------- full sweep

    [Fact]
    public void No_finding_message_carries_a_seeded_identifier()
    {
        var rpsFmt = AtihMatrix.Require("RPS");
        var rpsaFmt = AtihMatrix.Require("RPSA");
        var vidFmt = AtihMatrix.Require("VID-HOSP");

        var rps = Write("FV94_RPS_2024.txt",
            Line(rpsFmt.Length, (rpsFmt.IppStart, LeakIpp), (rpsFmt.DdnStart, LeakDdn)));
        var rpsa = Write("FV94_RPSA_2024.txt",
            Line(rpsaFmt.Length, (0, LeakFiness), (rpsaFmt.IppStart, LeakIpp)));
        var vid = Write("FV94_VID-HOSP_2024.txt",
            Line(vidFmt.Length, (0, LeakNir + "K7"), (vidFmt.IppStart, LeakOrphanIpp)));

        var findings = new CheckRunner().Run(new[]
        {
            (Path: rps, Format: "RPS"),
            (Path: rpsa, Format: "RPSA"),
            (Path: vid, Format: "VID-HOSP"),
        });

        Assert.NotEmpty(findings);

        string[] needles =
        {
            LeakNir, LeakIpp, LeakOrphanIpp, LeakDdn, LeakFiness,
            "7788990011", "286067511004", "9900112", _tempDir,
        };

        foreach (var finding in findings)
        {
            foreach (var needle in needles)
            {
                Assert.DoesNotContain(needle, finding.Message, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(needle, finding.FixHint ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
