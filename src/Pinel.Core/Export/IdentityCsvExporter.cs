using System.Globalization;
using System.Text;
using Pinel.Core.Identity;

namespace Pinel.Core.Processing;

/// <summary>
/// Exports the MPI to a CSV file. Port of the <c>export_csv</c> logic
/// from <c>DataProcessor</c>. The pivot DDN is the canonical value
/// injected when it is set.
/// </summary>
public static class IdentityCsvExporter
{
    public static async Task<int> ExportAsync(
        MasterPatientIndex mpi,
        string outputPath,
        char delimiter = ';',
        CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        await writer.WriteLineAsync(string.Join(delimiter, "IPP", "DDN_PIVOT", "DDN_OBSERVATIONS", "SOURCE_FILES"));

        int written = 0;
        foreach (var entry in mpi.All)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var observations = string.Join("|", entry.History.Keys);
            var sources = string.Join("|", entry.History.Values.SelectMany(v => v).Distinct(StringComparer.OrdinalIgnoreCase));
            var row = new[]
            {
                Escape(entry.Ipp, delimiter),
                Escape(entry.Pivot ?? string.Empty, delimiter),
                Escape(observations, delimiter),
                Escape(sources, delimiter),
            };
            await writer.WriteLineAsync(string.Join(delimiter, row));
            written++;
        }
        return written;
    }

    private static string Escape(string field, char delim)
    {
        // CSV formula injection guard: Excel interprets cells starting with
        // =, +, -, @, CR or tab as formulas. Prefix with a single quote to
        // neutralize them before the standard quoting logic.
        if (field.Length > 0 && "=+-@\t\r".IndexOf(field[0]) >= 0)
        {
            field = "'" + field;
        }
        if (field.Contains(delim) || field.Contains('"') || field.Contains('\n'))
        {
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        }
        return field;
    }
}
