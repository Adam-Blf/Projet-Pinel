using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Pinel.Core.Checks;
using Pinel.Core.Formats;
using Pinel.Core.Identity;
using Pinel.Core.Processing;
using Pinel.Core.Security;

namespace Pinel.Desktop.Api;

/// <summary>Points de terminaison d'identitovigilance : collisions d'IPP, choix du pivot, exports identité et assaini, et lancement des contrôles.</summary>
internal static class IdentityEndpoints
{
    internal static void Map(WebApplication app)
    {
        app.MapGet("/api/collisions", (PinelSession session) => Results.Ok(
            session.Processor.Mpi.Collisions.Select(e => new
            {
                ipp = e.Ipp,
                pivot = e.Pivot,
                options = e.History
                    .Select(kv => new { ddn = kv.Key, occurrences = kv.Value.Count, sources = kv.Value.Take(5) })
                    .OrderByDescending(o => o.occurrences),
                total = e.History.Values.Sum(s => s.Count),
            })));

        app.MapPost("/api/pivot", (PivotRequest r, PinelSession session) =>
        {
            if (string.IsNullOrWhiteSpace(r.Ipp) || string.IsNullOrWhiteSpace(r.Ddn))
            {
                return Results.BadRequest(new { error = "ipp et ddn requis" });
            }
            var entry = session.Processor.Mpi.Get(r.Ipp);
            if (entry is null) return Results.NotFound(new { error = "ipp inconnu" });
            entry.Pivot = r.Ddn;
            return Results.Ok(new { ipp = entry.Ipp, pivot = entry.Pivot });
        });

        app.MapPost("/api/export-identite", (PinelSession session) =>
        {
            if (LicenseGate.Refuse(session) is { } refus) return refus;

            var destination = SafePath.TryRequire(session.Settings.ResolveOutputFolder());
            if (destination is null) return Results.StatusCode(403);

            var output = Path.Combine(destination, $"identitovigilance-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
            var written = IdentityCsvExporter.ExportAsync(session.Processor.Mpi, output).GetAwaiter().GetResult();
            return Results.Ok(new { sortie = output, lignes = written });
        });

        app.MapPost("/api/export-assaini", (SanitizeRequest r, PinelSession session) =>
        {
            if (LicenseGate.Refuse(session) is { } refus) return refus;

            var source = SafePath.TryRequire(r.Fichier ?? string.Empty);
            if (source is null) return Results.StatusCode(403);
            if (!File.Exists(source)) return Results.NotFound(new { error = "fichier introuvable" });

            var format = AtihFormatIdentifier.Identify(source);
            if (format is null) return Results.BadRequest(new { error = "format non reconnu" });

            var destination = SafePath.TryRequire(session.Settings.ResolveOutputFolder());
            if (destination is null) return Results.StatusCode(403);

            var output = Path.Combine(destination, Path.GetFileNameWithoutExtension(source) + "_assaini.txt");
            var rewritten = new SanitizedExporter().Export(source, output, AtihMatrix.Require(format), session.Processor.Mpi);
            return Results.Ok(new { sortie = output, lignesReecrites = rewritten });
        });

        app.MapPost("/api/controles", (PinelSession session) =>
        {
            var findings = new CheckRunner().Run(
                session.Processor.Files
                    .Where(f => f.Format != "INCONNU")
                    .Select(f => (f.Path, f.Format)));

            return Results.Ok(new
            {
                synthese = CheckRunner.Summarize(findings),
                anomalies = findings,
            });
        });
    }
}

public sealed record SanitizeRequest(string? Fichier);
public sealed record PivotRequest(string? Ipp, string? Ddn);
