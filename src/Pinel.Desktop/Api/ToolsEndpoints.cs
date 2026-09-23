using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Pinel.Core.Excel;
using Pinel.Core.Processing;
using Pinel.Core.Security;
using Pinel.Desktop;

namespace Pinel.Desktop.Api;

/// <summary>Points de terminaison outils divers : inspection d'une ligne brute, aperçu d'un classeur Excel et rendu HTML vers PDF.</summary>
internal static class ToolsEndpoints
{
    internal static void Map(WebApplication app, BridgeHost.BridgeCallback? callback)
    {
        app.MapPost("/api/inspecter", (InspectRequest r) =>
        {
            var file = SafePath.TryRequire(r.Fichier ?? string.Empty);
            if (file is null) return Results.StatusCode(403);
            if (!File.Exists(file)) return Results.NotFound(new { error = "fichier introuvable" });

            var result = LineInspector.Inspect(file, r.Ligne);
            return result is null
                ? Results.NotFound(new { error = "ligne hors limites" })
                : Results.Ok(result);
        });

        app.MapPost("/api/excel/apercu", (ExcelRequest r) =>
        {
            var file = SafePath.TryRequire(r.Fichier ?? string.Empty);
            if (file is null) return Results.StatusCode(403);
            if (!File.Exists(file)) return Results.NotFound(new { error = "fichier introuvable" });

            try
            {
                return Results.Ok(ExcelReader.Preview(file, r.Lignes ?? 100));
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException)
            {
                // Le détail technique reste côté poste, il n'est pas renvoyé.
                return Results.BadRequest(new { error = "classeur illisible" });
            }
        });

        app.MapPost("/api/html-vers-pdf", async (PdfRequest r, PinelSession session) =>
        {
            if (callback is null) return Results.StatusCode(503);

            var html = SafePath.TryRequire(r.Html ?? string.Empty);
            if (html is null) return Results.StatusCode(403);
            if (!File.Exists(html)) return Results.NotFound(new { error = "page introuvable" });

            var destination = SafePath.TryRequire(session.Settings.ResolveOutputFolder());
            if (destination is null) return Results.StatusCode(403);

            var output = Path.Combine(destination,
                $"{Path.GetFileNameWithoutExtension(html)}-{DateTime.Now:yyyyMMdd-HHmmss}.pdf");

            var result = await callback(new BridgeRequest.HtmlToPdf(html, output, r.Paysage ?? false));
            return Results.Ok(result);
        });
    }
}

public sealed record InspectRequest(string? Fichier, int Ligne);
public sealed record ExcelRequest(string? Fichier, int? Lignes);
public sealed record PdfRequest(string? Html, bool? Paysage);
