using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Pinel.Core.Audit;
using Pinel.Core.Licensing;
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

        // Licence : etat et installation du fichier recu. La verification est
        // locale, aucun appel sortant, les postes n'ont pas internet.
        app.MapGet("/api/licence", (PinelSession session) =>
            Results.Ok(LicenseVerifier.Check(session.Settings.Etablissement.FinessEPmsi)));

        app.MapPost("/api/licence/importer", (FileRequest r, PinelSession session) =>
        {
            if (string.IsNullOrWhiteSpace(r.Fichier) || !File.Exists(r.Fichier))
            {
                return Results.NotFound(new { error = "fichier de licence introuvable" });
            }
            var status = LicenseVerifier.Install(r.Fichier, session.Settings.Etablissement.FinessEPmsi);
            AuditLogger.Instance.Record("/api/licence/importer", "POST", status.Valid ? 200 : 400, 0,
                status.Valid ? "licence installee" : "licence refusee");
            return Results.Ok(status);
        });

        // Mise a jour : etat, choix du dossier, installation. Aucune de ces
        // routes ne sort du poste, le dossier est un partage du GHT.
        app.MapGet("/api/maj", async (UpdateService updates) => Results.Ok(await updates.CheckAsync()));

        app.MapPost("/api/maj/dossier", (UpdateSourceRequest r, UpdateService updates) =>
        {
            updates.SetSource(r.Chemin);
            return Results.Ok(new { chemin = updates.Source });
        });

        app.MapPost("/api/maj/installer", async (UpdateService updates) => Results.Ok(await updates.ApplyAsync()));

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

/// <summary>Dossier des mises a jour, choisi par la direction des ressources numeriques.</summary>
public sealed record UpdateSourceRequest(string? Chemin);
