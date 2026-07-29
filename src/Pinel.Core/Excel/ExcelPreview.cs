using ClosedXML.Excel;

namespace Pinel.Core.Excel;

/// <summary>
/// Reads an .xlsx workbook and returns the first sheet's first N rows
/// as a flat matrix of strings. Used by the bridge <c>/api/import-excel</c>
/// endpoint so the TIM can preview FICHCOMP/FICUM templates, Excel-based
/// patient lists, or pivot tables exported from BigQuery dashboards.
/// </summary>
public static class ExcelReader
{
    /// <summary>
    /// Opens <paramref name="filePath"/> and returns sheet summaries plus
    /// the first <paramref name="maxRows"/> rows of the first sheet.
    /// Does not attempt full parsing - the goal is a lightweight preview.
    /// </summary>
    public static ExcelWorkbookPreview Preview(string filePath, int maxRows = 100)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Excel file not found: {filePath}", filePath);
        }

        using var workbook = new XLWorkbook(filePath);
        var sheets = workbook.Worksheets
            .Select(ws => new ExcelSheet(
                Name: ws.Name,
                RowCount: ws.LastRowUsed()?.RowNumber() ?? 0,
                ColumnCount: ws.LastColumnUsed()?.ColumnNumber() ?? 0))
            .ToList();

        var firstSheet = workbook.Worksheets.FirstOrDefault();
        if (firstSheet is null)
        {
            return new ExcelWorkbookPreview(filePath, sheets, null, Array.Empty<string>(), Array.Empty<IReadOnlyList<string>>());
        }

        var lastColumn = firstSheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        var lastRow = firstSheet.LastRowUsed()?.RowNumber() ?? 0;
        var takeRows = Math.Min(maxRows, lastRow);

        // Header row = first row with any value.
        var headers = new List<string>();
        for (int col = 1; col <= lastColumn; col++)
        {
            headers.Add(firstSheet.Cell(1, col).GetString());
        }

        var rows = new List<IReadOnlyList<string>>(takeRows);
        for (int r = 2; r <= takeRows + 1 && r <= lastRow; r++)
        {
            var row = new List<string>(lastColumn);
            for (int c = 1; c <= lastColumn; c++)
            {
                row.Add(firstSheet.Cell(r, c).GetString());
            }
            rows.Add(row);
        }

        return new ExcelWorkbookPreview(
            FilePath: filePath,
            Sheets: sheets,
            SelectedSheet: firstSheet.Name,
            Headers: headers,
            Rows: rows);
    }
}

public sealed record ExcelSheet(string Name, int RowCount, int ColumnCount);

public sealed record ExcelWorkbookPreview(
    string FilePath,
    IReadOnlyList<ExcelSheet> Sheets,
    string? SelectedSheet,
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string>> Rows);
