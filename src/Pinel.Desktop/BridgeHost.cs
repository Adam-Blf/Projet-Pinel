using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pinel.Core.Audit;
using Pinel.Core.Logging;
using Pinel.Desktop.Api;

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

        SystemEndpoints.Map(app);
        WorkspaceEndpoints.Map(app);
        MoulinetteEndpoints.Map(app);
        EpisodeEndpoints.Map(app);
        IdentityEndpoints.Map(app);
        StructureEndpoints.Map(app);
        FichcompEndpoints.Map(app);
        ToolsEndpoints.Map(app, callback);

        _ = Task.Run(() => app.RunAsync());
        return new HostedBridge(app, token);
    }

    private static string ResolveToken()
    {
        var env = Environment.GetEnvironmentVariable("PINEL_BRIDGE_TOKEN");
        return string.IsNullOrEmpty(env)
            ? Convert.ToHexString(RandomNumberGenerator.GetBytes(32))
            : env;
    }
}
