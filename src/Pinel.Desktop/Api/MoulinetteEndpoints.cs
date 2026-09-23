using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Pinel.Core.Export;
using Pinel.Core.Security;

namespace Pinel.Desktop.Api;

/// <summary>Points de terminaison de la moulinette à format : scan des fichiers, traitement complet et export CSV.</summary>
internal static class MoulinetteEndpoints
{
    internal static void Map(WebApplication app)
    {
        app.MapPost("/api/scanner", (PinelSession session) => Results.Ok(new
        {
            fichiers = session.Processor.Scan(),
            total = session.Processor.Files.Count,
        }));

        app.MapPost("/api/traiter", async (PinelSession session) =>
        {
            var totals = await Task.Run(() => session.Processor.ProcessAll());
            return Results.Ok(totals);
        });

        app.MapPost("/api/export-csv", (PinelSession session) =>
        {
            if (LicenseGate.Refuse(session) is { } refus) return refus;

            var destination = SafePath.TryRequire(session.Settings.ResolveOutputFolder());
            if (destination is null) return Results.StatusCode(403);

            var results = RecordCsvExporter.ExportBatch(
                session.Processor.Files
                    .Where(f => f.Format != "INCONNU")
                    .Select(f => (f.Path, f.Format)),
                destination,
                session.Layouts,
                FileYearResolver.Resolve);

            return Results.Ok(new
            {
                dossier = destination,
                fichiers = results.Select(r => new
                {
                    sortie = r.OutputPath,
                    format = r.Format,
                    lignes = r.Lines,
                    colonnes = r.Columns,
                    descriptif = r.LayoutSource,
                    brut = r.RawFallback,
                }),
            });
        });
    }
}
