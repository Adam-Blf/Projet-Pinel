using Pinel.Core.Structure;
using Xunit;

namespace Pinel.Tests;

public sealed class StructureParserTests : IDisposable
{
    private readonly string _tempDir;

    public StructureParserTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "sovereign_struct_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
    }

    [Theory]
    [InlineData("94G01", "G")]
    [InlineData("94I01", "I")]
    [InlineData("94D01", "D")]
    [InlineData("94P01", "P")]
    [InlineData("94Z01", "Z")]
    [InlineData("75-G-12", "G")]
    [InlineData("I01", "I")]
    [InlineData("NOT_A_SECTOR", null)]
    public void DetectSectorType_extracts_ars_letter(string code, string? expected)
    {
        Assert.Equal(expected, StructureParser.DetectSectorType(code));
    }

    [Fact]
    public void Parse_builds_tree_from_semicolon_csv()
    {
        var file = Path.Combine(_tempDir, "structure.csv");
        File.WriteAllText(file, """
LEVEL;CODE;PARENT;LABEL
Territoire;GHT;;GHT Sud Paris
Etablissement;FV;GHT;Fondation Vallée
Pôle;P01;FV;Pôle Adolescents
Secteur;94I01;P01;Secteur Gentilly
UM;94I01-U1;94I01;Unité A
""");

        var result = StructureParser.Parse(file);
        Assert.Equal("structure.csv", result.Filename);
        Assert.Single(result.Tree);
        Assert.Equal("GHT", result.Tree[0].Code);
        Assert.Equal(5, result.Summary.TotalNodes);
        Assert.Equal(5, result.Summary.MaxDepth);
        Assert.Contains("I", result.Summary.BySectorType.Keys);
    }

    [Fact]
    public void Parse_propagates_sector_type_to_children()
    {
        var file = Path.Combine(_tempDir, "struct.csv");
        File.WriteAllText(file, """
code;parent;label
94I01;;Secteur Gentilly
UM1;94I01;Unité sans code ARS
""");

        var result = StructureParser.Parse(file);
        var um = FindByCode(result.Tree, "UM1")!;
        Assert.Equal("I", um.SectorType);
        Assert.True(um.SectorTypeInherited);
    }

    [Fact]
    public void Parse_infers_sector_type_from_label_keywords()
    {
        var file = Path.Combine(_tempDir, "struct.csv");
        File.WriteAllText(file, """
code;parent;label
U1;;UMD Henri Colin
U2;;Unité UHSA Fresnes
U3;;Secteur infanto-juvénile
""");
        var result = StructureParser.Parse(file);
        var sectors = result.Tree.Select(n => n.SectorType).ToList();
        Assert.Contains("D", sectors);
        Assert.Contains("P", sectors);
        Assert.Contains("I", sectors);
    }

    private static StructureNode? FindByCode(IEnumerable<StructureNode> nodes, string code)
    {
        foreach (var n in nodes)
        {
            if (n.Code == code) return n;
            var child = FindByCode(n.Children, code);
            if (child is not null) return child;
        }
        return null;
    }
}
