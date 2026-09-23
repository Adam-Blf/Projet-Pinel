using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Pinel.Core.Fichcomp;
using Pinel.Core.Security;

namespace Pinel.Desktop.Api;

/// <summary>Points de terminaison FICHCOMP : nettoyage du classeur transports et contrôle de conformité au descriptif médicament/dispositif médical.</summary>
internal static class FichcompEndpoints
{
    internal static void Map(WebApplication app)
    {
        app.MapPost("/api/fichcomp/nettoyer", (FileRequest r, PinelSession session) =>
        {
            if (LicenseGate.Refuse(session) is { } refus) return refus;

            var file = SafePath.TryRequire(r.Fichier ?? string.Empty);
            if (file is null) return Results.StatusCode(403);
            if (!File.Exists(file)) return Results.NotFound(new { error = "fichier introuvable" });

            var destination = SafePath.TryRequire(session.Settings.ResolveOutputFolder());
            if (destination is null) return Results.StatusCode(403);

            var output = Path.Combine(destination, Path.GetFileNameWithoutExtension(file) + "_nettoye.xlsx");
            var result = TransportWorkbookCleaner.Clean(file, output);
            return Results.Ok(new
            {
                sortie = result.OutputPath,
                lignesLues = result.RowsRead,
                lignesRetirees = result.RowsRemoved,
                datesCompletees = result.DatesFilled,
            });
        });

        app.MapPost("/api/fichcomp/controler", (FichcompRequest r) =>
        {
            var file = SafePath.TryRequire(r.Fichier ?? string.Empty);
            if (file is null) return Results.StatusCode(403);
            if (!File.Exists(file)) return Results.NotFound(new { error = "fichier introuvable" });

            var layout = FichcompLayout.For(
                string.Equals(r.Type, "dmi", StringComparison.OrdinalIgnoreCase)
                    ? FichcompKind.DispositifMedical
                    : FichcompKind.Medicament);

            var result = FichcompConverter.Check(file, layout);
            return Results.Ok(new
            {
                type = layout.Label,
                lignes = result.Lines,
                conforme = result.IsClean,
                anomalies = result.Issues.Take(200),
            });
        });
    }
}

public sealed record FichcompRequest(string? Fichier, string? Type);
