using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Pinel.Core.Security;

namespace Pinel.Desktop.Api;

/// <summary>Points de terminaison de réglages des dossiers de travail : liste, ajout, retrait, dossier de sortie et dossier des descriptifs de formats.</summary>
internal static class WorkspaceEndpoints
{
    internal static void Map(WebApplication app)
    {
        app.MapGet("/api/reglages", (PinelSession session) => Results.Ok(new
        {
            dossiers = session.Settings.Folders,
            dossierSortie = session.Settings.ResolveOutputFolder(),
            dossierFormats = session.Settings.ResolveFormatsFolder(),
            dossiersAutorises = SafePath.Roots,
            etablissement = session.Settings.Etablissement,
        }));

        // Identite de l'etablissement : Pinel sert n'importe quel departement
        // d'information medicale, ces valeurs ne peuvent pas venir du code.
        app.MapPost("/api/reglages/etablissement", (EstablishmentSettings r, PinelSession session) =>
        {
            session.Settings.Etablissement = r;
            session.Settings.Save();
            return Results.Ok(session.Settings.Etablissement);
        });

        app.MapPost("/api/reglages/dossier", (FolderRequest r, PinelSession session) =>
        {
            var added = session.AddWorkFolder(r.Chemin ?? string.Empty);
            return added is null
                ? Results.BadRequest(new { error = "dossier introuvable ou illisible" })
                : Results.Ok(new { ajoute = added, dossiers = session.Settings.Folders });
        });

        app.MapDelete("/api/reglages/dossier", (FolderRequest r, PinelSession session) =>
        {
            session.RemoveWorkFolder(r.Chemin ?? string.Empty);
            return Results.Ok(new { dossiers = session.Settings.Folders });
        });

        app.MapPost("/api/reglages/sortie", (FolderRequest r, PinelSession session) =>
        {
            session.SetOutputFolder(r.Chemin ?? string.Empty);
            return Results.Ok(new { dossierSortie = session.Settings.ResolveOutputFolder() });
        });

        app.MapPost("/api/reglages/formats", (FolderRequest r, PinelSession session) =>
        {
            var count = session.SetFormatsFolder(r.Chemin ?? string.Empty);
            return Results.Ok(new { dossierFormats = session.Settings.ResolveFormatsFolder(), descriptifs = count });
        });
    }
}

public sealed record FolderRequest(string? Chemin);
