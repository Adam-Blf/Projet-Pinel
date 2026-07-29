using ClosedXML.Excel;
using Pinel.Core.Excel;
using Xunit;

namespace Pinel.Tests;

public sealed class ExcelPreviewTests : IDisposable
{
    private readonly string _tempDir;

    public ExcelPreviewTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "sovereign_excel_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
    }

    private string WriteWorkbook(string sheetName, params string[][] rows)
    {
        var file = Path.Combine(_tempDir, $"sample_{Guid.NewGuid():N}.xlsx");
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(sheetName);
        for (int r = 0; r < rows.Length; r++)
        {
            for (int c = 0; c < rows[r].Length; c++)
            {
                ws.Cell(r + 1, c + 1).Value = rows[r][c];
            }
        }
        wb.SaveAs(file);
        return file;
    }

    [Fact]
    public void Preview_returns_headers_and_rows()
    {
        var file = WriteWorkbook("Feuil1",
            new[] { "IPP", "DDN", "UM" },
            new[] { "IPP-A", "19900620", "94G1" },
            new[] { "IPP-B", "19800505", "94I1" });

        var preview = ExcelReader.Preview(file, 10);
        Assert.Equal("Feuil1", preview.SelectedSheet);
        Assert.Equal(new[] { "IPP", "DDN", "UM" }, preview.Headers);
        Assert.Equal(2, preview.Rows.Count);
        Assert.Equal("IPP-A", preview.Rows[0][0]);
    }

    [Fact]
    public void Preview_respects_max_rows()
    {
        var rows = Enumerable.Range(0, 20).Select(i => new[] { "r" + i }).ToArray();
        var all = new[] { new[] { "H1" } }.Concat(rows).ToArray();
        var file = WriteWorkbook("Feuil1", all);

        var preview = ExcelReader.Preview(file, 5);
        Assert.True(preview.Rows.Count <= 5);
    }

    [Fact]
    public void Preview_multiple_sheets_reports_them_all()
    {
        var file = Path.Combine(_tempDir, "multi.xlsx");
        using (var wb = new XLWorkbook())
        {
            wb.Worksheets.Add("Sheet1").Cell(1, 1).Value = "A";
            wb.Worksheets.Add("Sheet2").Cell(1, 1).Value = "B";
            wb.SaveAs(file);
        }

        var preview = ExcelReader.Preview(file, 10);
        Assert.Equal(2, preview.Sheets.Count);
        Assert.Contains(preview.Sheets, s => s.Name == "Sheet2");
    }

    [Fact]
    public void Preview_throws_if_file_missing()
    {
        Assert.Throws<FileNotFoundException>(() =>
            ExcelReader.Preview(Path.Combine(_tempDir, "nope.xlsx")));
    }
}
