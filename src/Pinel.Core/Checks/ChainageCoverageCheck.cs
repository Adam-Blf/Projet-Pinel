using System.Text;
using Pinel.Core.Formats;

namespace Pinel.Core.Checks;

/// <summary>
/// Cross-references IPPs between the activity files (RPS, RAA, RHS, RSS,
/// RPSS) and the chaînage files (VID-HOSP, and VID-IPP since 2023 for the
/// patients seen in ambulatory care). Surfaces two DRUIDES failure modes
/// documented by ATIH / lespmsi.com:
/// <list type="bullet">
///   <item><c>ERR-CHAINAGE-MANQUANT</c> - an IPP appears in activity files
///     but not in VID-HOSP - chaînage anonyme produit un code d'erreur
///     (020000 DDN manquante / 999999 NIR absent) côté SNDS.</item>
///   <item><c>WARN-CHAINAGE-ORPHAN</c> - un IPP présent dans VID-HOSP
///     n'est référencé par aucun fichier d'activité du lot - export
///     inutile, souvent résidu d'un envoi précédent.</item>
/// </list>
/// <para>
/// Mesure du 22/09/2026 sur un lot reel accepte par e-PMSI : en ne cherchant
/// que dans le VID-HOSP, le controle levait 162 454 erreurs sur les RAA, une
/// par ligne, alors que les patients vus en ambulatoire sont chaines par le
/// VID-IPP. La reference est desormais la reunion des deux fichiers, et un
/// patient manquant ne produit qu'une anomalie, a sa premiere ligne.
/// </para>
/// </summary>
public sealed class ChainageCoverageCheck : ICrossFileCheck
{
    private static readonly Encoding Latin1 = Encoding.GetEncoding("ISO-8859-1");
    private static readonly HashSet<string> ActivityFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "RPS", "RAA", "RPSA", "R3A", "RHS", "SSRHA", "RAPSS", "RAPSS-HAD", "RPSS", "RSS",
    };

    /// <summary>Upper bound on orphan findings, to keep the report readable.</summary>
    private const int OrphanCap = 200;

    /// <summary>Plafond des patients manquants detailles ; au-dela, un decompte.</summary>
    private const int MissingCap = 500;

    public string Name => "Chaînage VID-HOSP";

    static ChainageCoverageCheck()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public IEnumerable<CheckFinding> Validate(IReadOnlyList<(string Path, string Format)> files)
    {
        var vid = files.FirstOrDefault(f =>
            string.Equals(f.Format, "VID-HOSP", StringComparison.OrdinalIgnoreCase));
        var vidIpp = files.FirstOrDefault(f =>
            string.Equals(f.Format, "VID-IPP", StringComparison.OrdinalIgnoreCase));

        var activity = files
            .Where(f => ActivityFormats.Contains(f.Format))
            .ToList();

        if (vid.Path is null && vidIpp.Path is null && activity.Count == 0) yield break;

        if (vid.Path is null && vidIpp.Path is null)
        {
            yield return new CheckFinding(
                "ERR-CHAINAGE-VID-ABSENT", CheckSeverity.Error,
                "Fichier VID-HOSP manquant dans le lot. Aucun chaînage anonyme possible.",
                activity[0].Path, 0, activity[0].Format,
                FixHint: "Exporter le VID-HOSP depuis le SIH avant envoi DRUIDES.");
            yield break;
        }

        if (vid.Path is null)
        {
            // Lot purement ambulatoire : le VID-IPP suffit au chainage.
            vid = vidIpp;
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

        // Extract IPPs from VID-HOSP using its positional format, keeping the
        // line where each one was first seen: after redaction the line number is
        // the only locator left to the TIM, so it must be the real one.
        var vidFormat = AtihMatrix.Require(vid.Format);
        var vidIpps = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (ipp, lineNo) in ExtractIpps(vid.Path, vidFormat))
        {
            vidIpps.TryAdd(ipp, lineNo);
        }
        var chained = new HashSet<string>(vidIpps.Keys, StringComparer.Ordinal);
        if (vidIpp.Path is not null && vidIpp.Path != vid.Path)
        {
            foreach (var (ipp, _) in ExtractIpps(vidIpp.Path, AtihMatrix.Require("VID-IPP")))
            {
                chained.Add(ipp);
            }
        }

        // Cross-check each activity file.
        var activityIpps = new HashSet<string>(StringComparer.Ordinal);
        var reported = new HashSet<string>(StringComparer.Ordinal);
        int missingBeyondCap = 0;
        (string Path, string Format) lastActivity = activity[0];
        foreach (var (path, format) in activity)
        {
            if (!AtihMatrix.All.TryGetValue(format, out var fmt)) continue;
            var position = FindingRedaction.Position(fmt.IppStart, fmt.IppLength);
            foreach (var (ipp, lineNo) in ExtractIpps(path, fmt))
            {
                activityIpps.Add(ipp);
                if (!chained.Contains(ipp) && reported.Add(ipp))
                {
                    if (reported.Count > MissingCap)
                    {
                        missingBeyondCap++;
                        lastActivity = (path, format);
                        continue;
                    }
                    // Message carries the position, never the IPP: findings reach
                    // the UI and the JSON report on disk. See FindingRedaction.
                    yield return new CheckFinding(
                        "ERR-CHAINAGE-MANQUANT", CheckSeverity.Error,
                        $"IPP présent dans {format} ({position}) mais absent du VID-HOSP et du VID-IPP.",
                        path, lineNo, format,
                        FixHint: "Régénérer le VID-HOSP ou ajouter le patient au chaînage.");
                }
            }
        }

        if (missingBeyondCap > 0)
        {
            yield return new CheckFinding(
                "ERR-CHAINAGE-MANQUANT", CheckSeverity.Error,
                $"{missingBeyondCap} autre(s) patient(s) sans chaînage, non détaillés au-delà de {MissingCap}.",
                lastActivity.Path, 0, lastActivity.Format,
                FixHint: "Vérifier que le VID-HOSP et le VID-IPP du même envoi sont bien dans le lot.");
        }

        // Orphans in VID-HOSP that nobody in the activity set references.
        var vidPosition = FindingRedaction.Position(vidFormat.IppStart, vidFormat.IppLength);
        var orphans = vidIpps
            .Where(entry => !activityIpps.Contains(entry.Key))
            .OrderBy(entry => entry.Value)
            .Take(OrphanCap);
        foreach (var orphan in orphans)
        {
            yield return new CheckFinding(
                "WARN-CHAINAGE-ORPHAN", CheckSeverity.Warning,
                $"IPP présent dans VID-HOSP ({vidPosition}) mais absent des fichiers d'activité.",
                vid.Path, orphan.Value, vid.Format,
                FixHint: "Vérifier si c'est un résidu d'un envoi précédent à purger.");
        }
    }

    /// <summary>
    /// Yields every non-empty IPP of <paramref name="path"/> with its 1-indexed
    /// line number. A read failure yields nothing: the batch-level checks already
    /// raise an <c>ERR-IO</c> finding for the same file.
    /// </summary>
    private static IEnumerable<(string Ipp, int LineNumber)> ExtractIpps(string path, AtihFormat fmt)
    {
        StreamReader? reader = null;
        try { reader = new StreamReader(path, Latin1); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { yield break; }

        using (reader)
        {
            int lineNo = 0;
            while (reader.ReadLine() is { } line)
            {
                lineNo++;
                if (line.Length < fmt.IppEnd) continue;
                var ipp = line[fmt.IppStart..fmt.IppEnd].Trim();
                if (string.IsNullOrEmpty(ipp) || ipp.All(c => c == '0')) continue;
                yield return (ipp, lineNo);
            }
        }
    }
}
