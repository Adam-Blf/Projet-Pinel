using System.Text;
using Pinel.Core.Formats;
using Pinel.Core.Identity;

namespace Pinel.Core.Processing;

/// <summary>
/// Rewrites an ATIH .txt file with the pivot DDN injected and padding normalized.
/// Port of <c>DataProcessor.export_sanitized</c>. Latin-1 preserved.
/// </summary>
public sealed class SanitizedExporter
{
    private static readonly Encoding Latin1 = Encoding.GetEncoding("ISO-8859-1");

    static SanitizedExporter()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public int Export(string sourceFile, string outputFile, AtihFormat format, MasterPatientIndex mpi)
    {
        using var reader = new StreamReader(sourceFile, Latin1);
        using var writer = new StreamWriter(outputFile, append: false, Latin1);

        int rewritten = 0;
        while (reader.ReadLine() is { } line)
        {
            if (!AtihParser.IsLineValid(line))
            {
                writer.WriteLine(line);
                continue;
            }

            var repaired = AtihParser.AutoRepair(line, format.Length);
            if (repaired.Length < format.IppEnd || repaired.Length < format.DdnEnd)
            {
                writer.WriteLine(line);
                continue;
            }

            var ipp = AtihParser.NormalizeIpp(repaired[format.IppStart..format.IppEnd]);
            var entry = mpi.Get(ipp);
            if (entry?.Pivot is { Length: > 0 } pivot && pivot != repaired[format.DdnStart..format.DdnEnd])
            {
                var sb = new StringBuilder(repaired);
                // Pad or truncate pivot to the exact DDN width to keep fixed-width layout intact
                var ddnTarget = pivot.PadRight(format.DdnLength).Substring(0, format.DdnLength);
                for (int i = 0; i < ddnTarget.Length; i++)
                {
                    sb[format.DdnStart + i] = ddnTarget[i];
                }
                writer.WriteLine(sb.ToString());
                rewritten++;
            }
            else
            {
                writer.WriteLine(line);
            }
        }
        return rewritten;
    }
}
