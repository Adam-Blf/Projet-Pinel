using System.Text;
using Pinel.Core.Models;

namespace Pinel.Core.Formats;

/// <summary>
/// Extracts <see cref="PatientRecord"/>s from fixed-width ATIH files.
/// Port of the core extraction loop in <c>DataProcessor.process_file()</c>
/// from <c>backend/data_processor.py</c>.
/// </summary>
public sealed class AtihParser
{
    private const int MinLine = 50;
    private static readonly Encoding Latin1 = Encoding.GetEncoding("ISO-8859-1");

    static AtihParser()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>
    /// Parses a file and yields one <see cref="PatientRecord"/> per valid line.
    /// </summary>
    /// <param name="filePath">Path to the ATIH .txt file.</param>
    /// <param name="format">Format override. If null, identified from filename.</param>
    public IEnumerable<PatientRecord> Parse(string filePath, AtihFormat? format = null)
    {
        format ??= ResolveFormat(filePath);
        if (format is null)
        {
            yield break;
        }

        var effective = DetectVariant(filePath, format);

        using var reader = new StreamReader(filePath, Latin1);
        int lineNo = 0;
        while (reader.ReadLine() is { } line)
        {
            lineNo++;
            if (!IsLineValid(line)) continue;

            var repaired = AutoRepair(line, effective.Length);
            if (repaired.Length < effective.IppEnd || repaired.Length < effective.DdnEnd) continue;

            var ipp = NormalizeIpp(repaired[effective.IppStart..effective.IppEnd]);
            var ddn = repaired[effective.DdnStart..effective.DdnEnd].Trim();

            if (string.IsNullOrEmpty(ipp) || string.IsNullOrEmpty(ddn)) continue;

            yield return new PatientRecord(ipp, ddn, filePath, lineNo, format.Name);
        }
    }

    private static AtihFormat? ResolveFormat(string filePath)
    {
        var name = AtihFormatIdentifier.Identify(filePath);
        return name is null ? null : AtihMatrix.Require(name);
    }

    /// <summary>
    /// Picks the best matching variant for legacy files whose line length
    /// differs from the canonical <paramref name="format"/>.
    /// Samples the first 100 valid lines to detect dominant length.
    /// </summary>
    internal static AtihFormat DetectVariant(string filePath, AtihFormat format)
    {
        if (!AtihMatrix.Variants.TryGetValue(format.Name, out var variants) || variants.Count == 0)
        {
            return format;
        }

        var lengths = new Dictionary<int, int>();
        try
        {
            using var reader = new StreamReader(filePath, Latin1);
            int sampled = 0;
            while (sampled < 100 && reader.ReadLine() is { } line)
            {
                if (!IsLineValid(line)) continue;
                var l = line.TrimEnd().Length;
                lengths[l] = lengths.GetValueOrDefault(l) + 1;
                sampled++;
            }
        }
        catch (IOException)
        {
            return format;
        }

        if (lengths.Count == 0) return format;
        var dominant = lengths.OrderByDescending(kv => kv.Value).First().Key;

        // Canonical length wins if present
        if (dominant == format.Length) return format;

        foreach (var v in variants)
        {
            if (v.Length == dominant)
            {
                return format with
                {
                    Length = v.Length,
                    IppStart = v.IppStart,
                    IppEnd = v.IppEnd,
                    DdnStart = v.DdnStart,
                    DdnEnd = v.DdnEnd,
                };
            }
        }
        return format;
    }

    internal static bool IsLineValid(string line)
    {
        if (line.Length < MinLine) return false;
        foreach (var c in line)
        {
            if (c != '0' && c != ' ') return true;
        }
        return false;
    }

    /// <summary>
    /// Normalizes an IPP by trimming, stripping spaces, and left-padding with zeros
    /// if purely numeric. Keeps alpha-numeric IPPs untouched (Fondation Vallée legacy).
    /// </summary>
    internal static string NormalizeIpp(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.Length == 0) return string.Empty;
        return trimmed.Replace(" ", string.Empty);
    }

    /// <summary>
    /// Extends or truncates a line to the expected width, so that positional
    /// extraction never crashes on slightly malformed records.
    /// </summary>
    internal static string AutoRepair(string line, int expectedLength)
    {
        if (line.Length == expectedLength) return line;
        if (line.Length > expectedLength) return line[..expectedLength];
        return line.PadRight(expectedLength, ' ');
    }
}
