using Pinel.Core.Formats;
using Xunit;

namespace Pinel.Tests;

/// <summary>
/// Spot checks on format detection - covers the priority-sensitive overlaps
/// like RPSA before RPS and R3A before RAA.
/// </summary>
public sealed class AtihFormatIdentifierTests
{
    [Theory]
    [InlineData("FV94_RPS_2024.txt", "RPS")]
    [InlineData("FV94_RPSA_2024.txt", "RPSA")]
    [InlineData("FV94_RAA_2024.txt", "RAA")]
    [InlineData("FV94_R3A_2024.txt", "R3A")]
    [InlineData("VID-HOSP_2024.txt", "VID-HOSP")]
    [InlineData("ANO-HOSP_2024.txt", "ANO-HOSP")]
    [InlineData("FICHSUP_PSY_2024.txt", "FICHSUP-PSY")]
    [InlineData("FICUM-PSY_2024.txt", "FICUM-PSY")]
    [InlineData("rsfa_export.dat", "RSFA")]
    [InlineData("RSFB_seasons.txt", "RSFB")]
    [InlineData("RSFC_honoraires.txt", "RSFC")]
    [InlineData("RHS_2024_Q3.txt", "RHS")]
    [InlineData("rapss_had_2024.txt", "RAPSS-HAD")]
    [InlineData("RSS_2024.txt", "RSS")]
    [InlineData("UNKNOWN_FORMAT.xlsx", null)]
    // Noms reels des exports Druides, lots 2026 du DIM.
    [InlineData("PSY_RAA_HOSP_PMSI_20260513110217.txt", "HOSP-PMSI")]
    [InlineData("MCO_HOSP_PMSI_20260205113422 - Corriger.txt", "HOSP-PMSI")]
    [InlineData("vh_psy.txt", "VID-HOSP")]
    [InlineData("vh_psy_CORR_main.txt", "VID-HOSP")]
    [InlineData("vipp_CORRIGE_main.txt", "VID-IPP")]
    [InlineData("ipp_202605131120.txt", "VID-IPP")]
    [InlineData("fc_ic.txt", "FICHCOMP-ISO")]
    [InlineData("iso_202605131120.txt", "FICHCOMP-ISO")]
    [InlineData("fc_htpart.txt", "FICHCOMP-TP")]
    [InlineData("tp_202605131120.txt", "FICHCOMP-TP")]
    [InlineData("rps_202605131120.txt", "RPS")]
    [InlineData("raa_202605131120.txt", "RAA")]
    public void Identify_returns_expected_format(string filename, string? expected)
    {
        Assert.Equal(expected, AtihFormatIdentifier.Identify(filename));
    }

    [Fact]
    public void Matrix_contains_the_22_verified_formats()
    {
        // 22 formats le 28/08/2026 (EDGAR retire : typologie d'actes codee dans
        // le RAA, pas un format de fichier), 27 le 22/09/2026 avec VID-IPP,
        // HOSP-PMSI, HOSP-FACT et les FICHCOMP isolement et temps partiel.
        Assert.Equal(27, AtihMatrix.All.Count);
    }

    [Fact]
    public void Field_categories_are_exhaustive()
    {
        var fields = AtihMatrix.All.Values.Select(f => f.Field).Distinct().ToHashSet();
        Assert.Contains("PSY", fields);
        Assert.Contains("MCO", fields);
        Assert.Contains("SSR", fields);
        Assert.Contains("HAD", fields);
        Assert.Contains("TRANSVERSAL", fields);
    }
}
