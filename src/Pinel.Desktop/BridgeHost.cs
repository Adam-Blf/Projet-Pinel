using System.IO;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pinel.Core.Audit;
using Pinel.Core.Checks;
using Pinel.Core.Episodes;
using Pinel.Core.Excel;
using Pinel.Core.Export;
using Pinel.Core.Fichcomp;
using Pinel.Core.Formats;
using Pinel.Core.Identity;
using Pinel.Core.Logging;
using Pinel.Core.Processing;
using Pinel.Core.Security;
using Pinel.Core.Structure;

namespace Pinel.Desktop;

/// <summary>
/// Service HTTP local qui expose le cœur métier à l'interface, dans le même
/// processus que la fenêtre.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>écoute uniquement sur 127.0.0.1, jamais sur une interface réseau ;</item>
///   <item>jeton de session aléatoire exigé sur toutes les routes sauf le test
///     de disponibilité ;</item>
///   <item>liste blanche de l'en-tête Host, origines restreintes ;</item>
///   <item>toute opération sur un fichier passe par <see cref="SafePath"/> ;</item>
///   <item>les opérations sensibles sont tracées dans le journal d'audit, sans
///     aucune donnée patient.</item>
/// </list>
/// </remarks>
public static class BridgeHost
{
    public sealed record HostedBridge(WebApplication App, string Token);

    /// <summary>Appels qui doivent repasser par le fil d'exécution de l'interface.</summary>
    public delegate Task<object?> BridgeCallback(BridgeRequest request);

    public static HostedBridge Start(int port, PinelSession session, BridgeCallback? callback = null)
    {
        var token = ResolveToken();
        var allowedHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            $"127.0.0.1:{port}",
            $"localhost:{port}",
        };

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = Environments.Production,
        });

        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
            policy.WithOrigins("http://pinel.local")
                  .WithHeaders("Content-Type", "Authorization")
                  .WithMethods("GET", "POST", "DELETE")));
        builder.Services.AddSingleton(session);
        builder.Services.AddSingleton<LogBuffer>();

        var app = builder.Build();

        app.Use(async (ctx, next) =>
        {
            if (!allowedHosts.Contains(ctx.Request.Headers.Host.ToString()))
            {
                ctx.Response.StatusCode = 421;
                return;
            }
            ctx.Response.Headers["Cache-Control"] = "no-store";
            ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
            await next();
        });

        app.Use(async (ctx, next) =>
        {
            if (ctx.Request.Path == "/health")
            {
                await next();
                return;
            }
            if (!string.Equals(ctx.Request.Headers.Authorization.ToString(), $"Bearer {token}", StringComparison.Ordinal))
            {
                ctx.Response.StatusCode = 401;
                await ctx.Response.WriteAsJsonAsync(new { error = "non autorisé" });
                return;
            }
            await next();
        });

        app.UseCors();

        var audited = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "/api/scanner", "/api/traiter", "/api/export-csv", "/api/episodes",
            "/api/collisions", "/api/pivot", "/api/export-identite", "/api/export-assaini",
            "/api/controles", "/api/structure", "/api/structure/export",
            "/api/fichcomp/nettoyer", "/api/fichcomp/controler",
            "/api/inspecter", "/api/excel/apercu", "/api/html-vers-pdf",
        };
        app.Use(async (ctx, next) =>
        {
            await next();
            if (audited.Contains(ctx.Request.Path.Value ?? string.Empty))
            {
                AuditLogger.Instance.Record(
                    endpoint: ctx.Request.Path.Value ?? "?",
                    method: ctx.Request.Method,
                    status: ctx.Response.StatusCode,
                    bytes: ctx.Response.ContentLength);
            }
        });

        MapSystem(app);
        MapWorkspace(app);
        MapMoulinette(app);
        MapEpisodes(app);
        MapIdentity(app);
        MapStructure(app);
        MapFichcomp(app);
        MapTools(app, callback);

        _ = Task.Run(() => app.RunAsync());
        return new HostedBridge(app, token);
    }

    // ── Système ────────────────────────────────────────────────────────────
    private static void MapSystem(WebApplication app)
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

    // ── Dossiers de travail ────────────────────────────────────────────────
    private static void MapWorkspace(WebApplication app)
    {
        app.MapGet("/api/reglages", (PinelSession session) => Results.Ok(new
        {
            dossiers = session.Settings.Folders,
            dossierSortie = session.Settings.ResolveOutputFolder(),
            dossierFormats = session.Settings.ResolveFormatsFolder(),
            dossiersAutorises = SafePath.Roots,
        }));

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

    // ── Chantier 1 : moulinette à format ───────────────────────────────────
    private static void MapMoulinette(WebApplication app)
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
            var destination = SafePath.TryRequire(session.Settings.ResolveOutputFolder());
            if (destination is null) return Results.StatusCode(403);

            var results = RecordCsvExporter.ExportBatch(
                session.Processor.Files
                    .Where(f => f.Format != "INCONNU")
                    .Select(f => (f.Path, f.Format)),
                destination,
                session.Layouts,
                FileYear);

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

    // ── Chantier 2 : épisodes ambulatoires ─────────────────────────────────
    private static void MapEpisodes(WebApplication app)
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
                var layout = session.Layouts.Resolve(file.Format, FileYear(file.Path));
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

    // ── Identitovigilance ──────────────────────────────────────────────────
    private static void MapIdentity(WebApplication app)
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
            var destination = SafePath.TryRequire(session.Settings.ResolveOutputFolder());
            if (destination is null) return Results.StatusCode(403);

            var output = Path.Combine(destination, $"identitovigilance-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
            var written = IdentityCsvExporter.ExportAsync(session.Processor.Mpi, output).GetAwaiter().GetResult();
            return Results.Ok(new { sortie = output, lignes = written });
        });

        app.MapPost("/api/export-assaini", (SanitizeRequest r, PinelSession session) =>
        {
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

    // ── Chantier 3 : structure FICOM ───────────────────────────────────────
    private static void MapStructure(WebApplication app)
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

    // ── FICHCOMP, moulinette Excel ─────────────────────────────────────────
    private static void MapFichcomp(WebApplication app)
    {
        app.MapPost("/api/fichcomp/nettoyer", (FileRequest r, PinelSession session) =>
        {
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

    // ── Outils ─────────────────────────────────────────────────────────────
    private static void MapTools(WebApplication app, BridgeCallback? callback)
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

    /// <summary>Année portée par le nom de fichier, pour choisir le bon descriptif.</summary>
    private static int? FileYear(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        for (int i = 0; i + 4 <= name.Length; i++)
        {
            if (int.TryParse(name.AsSpan(i, 4), out var year) && year is >= 2000 and <= 2100)
            {
                return year;
            }
        }
        return null;
    }

    private static string ResolveToken()
    {
        var env = Environment.GetEnvironmentVariable("PINEL_BRIDGE_TOKEN");
        return string.IsNullOrEmpty(env)
            ? Convert.ToHexString(RandomNumberGenerator.GetBytes(32))
            : env;
    }
}

public sealed record FolderRequest(string? Chemin);
public sealed record FileRequest(string? Fichier);
public sealed record SanitizeRequest(string? Fichier);
public sealed record PivotRequest(string? Ipp, string? Ddn);
public sealed record InspectRequest(string? Fichier, int Ligne);
public sealed record ExcelRequest(string? Fichier, int? Lignes);
public sealed record FichcompRequest(string? Fichier, string? Type);
public sealed record PdfRequest(string? Html, bool? Paysage);
public sealed record EpisodeRequest(int? ToleranceJours, bool? RuptureUm, bool? RuptureSite, bool? FermetureAnnuelle);
