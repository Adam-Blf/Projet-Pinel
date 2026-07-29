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
    public void Identify_returns_expected_format(string filename, string? expected)
    {
        Assert.Equal(expected, AtihFormatIdentifier.Identify(filename));
    }

    [Fact]
    public void Matrix_contains_all_23_core_formats()
    {
        // 23 canonical formats (matrix keeps the 23 live ones after de-dup).
        Assert.Equal(23, AtihMatrix.All.Count);
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
