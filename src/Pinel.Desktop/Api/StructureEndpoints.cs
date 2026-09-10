using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Pinel.Core.Export;
using Pinel.Core.Security;
using Pinel.Core.Structure;

namespace Pinel.Desktop.Api;

/// <summary>Points de terminaison de l'analyse de structure FICOM : lecture d'un fichier et export CSV de la structure relevée.</summary>
internal static class StructureEndpoints
{
    internal static void Map(WebApplication app)
    {
        app.MapPost("/api/structure", (FileRequest r) =>
        {
            var file = SafePath.TryRequire(r.Fichier ?? string.Empty);
            if (file is null) return Results.StatusCode(403);
            if (!File.Exists(file)) return Results.NotFound(new { error = "fichier introuvable" });
            return Results.Ok(StructureParser.Parse(file));
        });

        app.MapPost("/api/structure/export", (FileRequest r, PinelSession session) =>
        {
            var file = SafePath.TryRequire(r.Fichier ?? string.Empty);
            if (file is null) return Results.StatusCode(403);
            if (!File.Exists(file)) return Results.NotFound(new { error = "fichier introuvable" });

            var destination = SafePath.TryRequire(session.Settings.ResolveOutputFolder());
            if (destination is null) return Results.StatusCode(403);

            var result = StructureParser.Parse(file);
            var output = OutputPath.Unique(destination,
                Path.GetFileNameWithoutExtension(file) + "_structure", ".csv");
            var lines = StructureCsvExporter.Export(result, output);
            return Results.Ok(new { sortie = output, lignes = lines });
        });
    }
}

public sealed record FileRequest(string? Fichier);
