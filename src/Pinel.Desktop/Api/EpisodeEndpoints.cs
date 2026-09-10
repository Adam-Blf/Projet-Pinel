using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Pinel.Core.Episodes;
using Pinel.Core.Security;

namespace Pinel.Desktop.Api;

/// <summary>Point de terminaison des épisodes ambulatoires : regroupement des venues RAA/R3A en épisodes et export CSV.</summary>
internal static class EpisodeEndpoints
{
    internal static void Map(WebApplication app)
    {
        app.MapPost("/api/episodes", (EpisodeRequest r, PinelSession session) =>
        {
            var rules = new EpisodeRules(
                MaxGapDays: Math.Clamp(r.ToleranceJours ?? 0, 0, 30),
                SplitOnUmChange: r.RuptureUm ?? true,
                SplitOnSiteChange: r.RuptureSite ?? true,
                CloseAtYearEnd: r.FermetureAnnuelle ?? true);

            var visits = new List<AmbulatoryVisit>();
            var ignored = new List<string>();

            // Seuls les formats ambulatoires entrent dans le calcul. Le RPS
            // decrit l'hospitalisation complete : l'y inclure fabriquerait des
            // durees de presence qui ne veulent rien dire.
            foreach (var file in session.Processor.Files.Where(f => f.Format is "RAA" or "R3A"))
            {
                var layout = session.Layouts.Resolve(file.Format, FileYearResolver.Resolve(file.Path));
                if (layout is null || !EpisodeReader.IsUsable(layout))
                {
                    ignored.Add(file.Name);
                    continue;
                }
                visits.AddRange(EpisodeReader.Read(file.Path, layout));
            }

            var episodes = EpisodeBuilder.Build(visits, rules);

            string? output = null;
            if (episodes.Count > 0)
            {
                var destination = SafePath.TryRequire(session.Settings.ResolveOutputFolder());
                if (destination is not null)
                {
                    output = Path.Combine(destination, $"episodes-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
                    EpisodeCsvExporter.Export(episodes, output, rules);
                }
            }

            return Results.Ok(new
            {
                regle = rules.Describe(),
                venues = visits.Count,
                episodes = episodes.Count,
                dureeMoyenne = episodes.Count == 0 ? 0 : Math.Round(episodes.Average(e => e.DurationDays), 2),
                superieurs1Jour = episodes.Count(e => e.DurationDays > 1),
                fichiersIgnores = ignored,
                sortie = output,
                apercu = episodes.Take(50).Select(e => new
                {
                    id = e.EpisodeId,
                    ipp = e.Ipp,
                    um = e.Um,
                    site = e.Site,
                    debut = e.Start.ToString("yyyy-MM-dd"),
                    fin = e.End.ToString("yyyy-MM-dd"),
                    venues = e.VisitCount,
                    duree = e.DurationDays,
                }),
            });
        });
    }
}

public sealed record EpisodeRequest(int? ToleranceJours, bool? RuptureUm, bool? RuptureSite, bool? FermetureAnnuelle);
