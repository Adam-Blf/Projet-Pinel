using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Pinel.Core.Audit;
using Pinel.Core.Formats;
using Pinel.Core.Logging;

namespace Pinel.Desktop.Api;

/// <summary>Points de terminaison système : vie du service, journal, audit, réinitialisation de session et liste des formats connus.</summary>
internal static class SystemEndpoints
{
    internal static void Map(WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        app.MapGet("/api/journal", (LogBuffer logs) => Results.Ok(logs.Drain()));

        app.MapGet("/api/audit", () => Results.Ok(new { chemin = AuditLogger.Instance.CurrentLogPath }));

        app.MapPost("/api/reinitialiser", (PinelSession session) =>
        {
            session.Reset();
            return Results.Ok(new { reinitialise = true });
        });

        app.MapGet("/api/formats", (PinelSession session) => Results.Ok(
            AtihMatrix.All.Values.Select(f => new
            {
                nom = f.Name,
                domaine = f.Field,
                longueur = f.Length,
                description = f.Description,
                descriptif = session.Layouts.Resolve(f.Name)?.Source ?? string.Empty,
                champs = session.Layouts.Resolve(f.Name)?.Fields.Count ?? 0,
            })));
    }
}
