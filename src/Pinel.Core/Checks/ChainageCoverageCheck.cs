using System.Text;
using Pinel.Core.Formats;

namespace Pinel.Core.Checks;

/// <summary>
/// Cross-references IPPs between the activity files (RPS, RAA, RHS, RSS,
/// RPSS) and the chaînage file (VID-HOSP). Surfaces two DRUIDES failure
/// modes documented by ATIH / lespmsi.com:
/// <list type="bullet">
///   <item><c>ERR-CHAINAGE-MANQUANT</c> - an IPP appears in activity files
///     but not in VID-HOSP - chaînage anonyme produit un code d'erreur
///     (020000 DDN manquante / 999999 NIR absent) côté SNDS.</item>
///   <item><c>WARN-CHAINAGE-ORPHAN</c> - un IPP présent dans VID-HOSP
///     n'est référencé par aucun fichier d'activité du lot - export
///     inutile, souvent résidu d'un envoi précédent.</item>
/// </list>
/// </summary>
public sealed class ChainageCoverageCheck : ICrossFileCheck
{
    private static readonly Encoding Latin1 = Encoding.GetEncoding("ISO-8859-1");
    private static readonly HashSet<string> ActivityFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "RPS", "RAA", "RPSA", "R3A", "RHS", "SSRHA", "RAPSS", "RAPSS-HAD", "RPSS", "RSS",
    };

    public string Name => "Chaînage VID-HOSP";

    static ChainageCoverageCheck()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public IEnumerable<CheckFinding> Validate(IReadOnlyList<(string Path, string Format)> files)
    {
        var vid = files.FirstOrDefault(f =>
            string.Equals(f.Format, "VID-HOSP", StringComparison.OrdinalIgnoreCase));

        var activity = files
            .Where(f => ActivityFormats.Contains(f.Format))
            .ToList();

        if (vid.Path is null && activity.Count == 0) yield break;

        if (vid.Path is null)
        {
            yield return new CheckFinding(
                "ERR-CHAINAGE-VID-ABSENT", CheckSeverity.Error,
                "Fichier VID-HOSP manquant dans le lot. Aucun chaînage anonyme possible.",
                activity[0].Path, 0, activity[0].Format,
                FixHint: "Exporter le VID-HOSP depuis le SIH avant envoi DRUIDES.");
            yield break;
        }

        if (activity.Count == 0)
        {
            yield return new CheckFinding(
                "WARN-CHAINAGE-NO-ACTIVITY", CheckSeverity.Warning,
                "VID-HOSP présent mais aucun fichier d'activité (RPS/RAA/RHS/RPSS) à chaîner.",
                vid.Path, 0, vid.Format,
                FixHint: "Ajouter les fichiers d'activité au lot, ou retirer le VID-HOSP.");
            yield break;
        }

        // Extract IPPs from VID-HOSP using its positional format.
        var vidFormat = AtihMatrix.Require("VID-HOSP");
        var vidIpps = new HashSet<string>(StringComparer.Ordinal);
        foreach (var ipp in ExtractIpps(vid.Path, vidFormat))
        {
            vidIpps.Add(ipp);
        }

        // Cross-check each activity file.
        var activityIpps = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (path, format) in activity)
        {
            if (!AtihMatrix.All.TryGetValue(format, out var fmt)) continue;
            int lineNo = 0;
            foreach (var ipp in ExtractIpps(path, fmt, withLineNumbers: true))
            {
                lineNo++;
                activityIpps.Add(ipp);
                if (!vidIpps.Contains(ipp))
                {
                    yield return new CheckFinding(
                        "ERR-CHAINAGE-MANQUANT", CheckSeverity.Error,
                        $"IPP « {ipp} » présent dans {format} mais absent du VID-HOSP.",
                        path, lineNo, format,
                        FixHint: "Régénérer le VID-HOSP ou ajouter le patient au chaînage.");
                }
            }
        }

        // Orphans in VID-HOSP that nobody in the activity set references.
        foreach (var orphan in vidIpps.Except(activityIpps).Take(200))
        {
            yield return new CheckFinding(
                "WARN-CHAINAGE-ORPHAN", CheckSeverity.Warning,
                $"IPP « {orphan} » présent dans VID-HOSP mais absent des fichiers d'activité.",
                vid.Path, 0, vid.Format,
                FixHint: "Vérifier si c'est un résidu d'un envoi précédent à purger.");
        }
    }

    private static IEnumerable<string> ExtractIpps(string path, AtihFormat fmt, bool withLineNumbers = false)
    {
        StreamReader? reader = null;
        try { reader = new StreamReader(path, Latin1); }
        catch (IOException) { yield break; }

        using (reader)
        {
            while (reader.ReadLine() is { } line)
            {
                if (line.Length < fmt.IppEnd) continue;
                var ipp = line[fmt.IppStart..fmt.IppEnd].Trim();
                if (string.IsNullOrEmpty(ipp) || ipp.All(c => c == '0')) continue;
                yield return ipp;
            }
        }
    }
}
