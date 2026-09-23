using System.Text;
using Pinel.Core.Formats;

namespace Pinel.Core.Processing;

/// <summary>
/// Returns the positional field breakdown of a specific line of an ATIH
/// file. Used by the <c>/api/inspect</c> endpoint so that a TIM can
/// diagnose a DRUIDES rejection by clicking on a line in the frontend
/// grid and seeing which bytes correspond to IPP, DDN, UM, DP, etc.
/// </summary>
public static class LineInspector
{


    /// <summary>
    /// Loads line <paramref name="lineNumber"/> (1-indexed) from
    /// <paramref name="filePath"/> and extracts the documented fields.
    /// Returns null if the file/line is missing or the format is unknown.
    /// </summary>
    public static InspectionResult? Inspect(string filePath, int lineNumber)
    {
        if (lineNumber < 1 || !File.Exists(filePath)) return null;

        var formatName = AtihFormatIdentifier.Identify(filePath);
        if (formatName is null) return new InspectionResult(
            FilePath: filePath,
            LineNumber: lineNumber,
            Format: null,
            RawLine: null,
            Fields: new Dictionary<string, string>());

        string? rawLine = null;
        using (var reader = new StreamReader(filePath, PmsiEncoding.Latin1))
        {
            int current = 0;
            while (reader.ReadLine() is { } line)
            {
                current++;
                if (current == lineNumber)
                {
                    rawLine = line;
                    break;
                }
            }
        }

        if (rawLine is null) return null;

        var fields = new Dictionary<string, string>();
        if (AtihMatrix.All.TryGetValue(formatName, out var fmt))
        {
            fields["format"] = fmt.Name;
            fields["length"] = fmt.Length.ToString();
            fields["line_length"] = rawLine.Length.ToString();
            fields["ipp"] = SafeSlice(rawLine, fmt.IppStart, fmt.IppEnd);
            fields["ddn"] = SafeSlice(rawLine, fmt.DdnStart, fmt.DdnEnd);
            fields["field"] = fmt.Field;

            // Best-effort format-specific extractions.
            switch (formatName)
            {
                case "RPS":
                case "RPSA":
                    fields["finess_juridique"] = SafeSlice(rawLine, 0, 9);
                    fields["finess_geographique"] = SafeSlice(rawLine, 9, 18);
                    fields["date_sortie_sejour"] = SafeSlice(rawLine, 88, 96);
                    fields["um"] = SafeSlice(rawLine, 98, 102);
                    fields["mode_legal"] = rawLine.Length > 106 ? rawLine[106].ToString() : "";
                    fields["date_debut_seq"] = SafeSlice(rawLine, 109, 117);
                    fields["date_fin_seq"] = SafeSlice(rawLine, 117, 125);
                    fields["nb_jours_presence"] = SafeSlice(rawLine, 125, 128);
                    fields["diagnostic_principal"] = SafeSlice(rawLine, 140, 148);
                    break;

                case "FICHSUP-PSY":
                    fields["n_sejour"] = SafeSlice(rawLine, 11, 31);
                    fields["type_mesure"] = rawLine.Length > 38 ? rawLine[38].ToString() : "";
                    fields["date_debut_mesure"] = SafeSlice(rawLine, 39, 47);
                    fields["date_fin_mesure"] = SafeSlice(rawLine, 51, 59);
                    break;

                case "VID-HOSP":
                    fields["nir"] = SafeSlice(rawLine, 0, 13);
                    fields["cle_nir"] = SafeSlice(rawLine, 13, 15);
                    break;

                case "FICUM-PSY":
                    fields["header"] = SafeSlice(rawLine, 0, 5);
                    fields["code_um"] = SafeSlice(rawLine, 5, 18);
                    break;
            }
        }

        return new InspectionResult(
            FilePath: filePath,
            LineNumber: lineNumber,
            Format: formatName,
            RawLine: rawLine,
            Fields: fields);
    }

    private static string SafeSlice(string s, int start, int end)
    {
        if (start >= s.Length) return string.Empty;
        var e = Math.Min(end, s.Length);
        return s[start..e];
    }
}

/// <summary>Response payload for <c>/api/inspect</c>.</summary>
public sealed record InspectionResult(
    string FilePath,
    int LineNumber,
    string? Format,
    string? RawLine,
    IReadOnlyDictionary<string, string> Fields);
