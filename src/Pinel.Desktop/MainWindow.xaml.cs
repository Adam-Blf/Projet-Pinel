using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Pinel.Core.Security;
using Pinel.Desktop.Pdf;

namespace Pinel.Desktop;

/// <summary>
/// Single-process host for the Pinel app.
/// Hosts:
/// <list type="bullet">
///   <item>The WPF main window.</item>
///   <item>A WebView2 control that renders the existing HTML/CSS/JS frontend.</item>
///   <item>A hidden WebView2 dedicated to HTML → PDF conversion.</item>
///   <item>An in-process ASP.NET Core bridge (<see cref="BridgeHost"/>) on 127.0.0.1:8787.</item>
/// </list>
/// All of the above ship as a single self-contained .exe.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>TCP port where the in-process bridge listens (loopback only).</summary>
    private const int BridgePort = 8787;

    /// <summary>
    /// Shared <see cref="HttpClient"/> used only for the bridge readiness
    /// probe. Static so we never leak sockets across reloads.
    /// </summary>
    private static readonly HttpClient HealthClient = new() { Timeout = TimeSpan.FromMilliseconds(500) };

    /// <summary>État de session partagé avec le service HTTP local.</summary>
    private readonly PinelSession _session = new();

    /// <summary>Wrapper around the running <see cref="Microsoft.AspNetCore.Builder.WebApplication"/> + its Bearer token.</summary>
    private BridgeHost.HostedBridge? _bridge;

    /// <summary>Lazily instantiated when the bridge asks for an HTML → PDF conversion.</summary>
    private PdfRenderer? _pdfRenderer;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    /// <summary>
    /// Entry point after XAML has been inflated. Starts the HTTP bridge,
    /// waits for it to accept connections, initializes the WebView2,
    /// and wires the JS ↔ native bridge for dialogs and PDF rendering.
    /// </summary>
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Cree l'espace de travail des le demarrage : l'utilisateur le trouve
        // a l'emplacement annonce par la documentation sans avoir a exporter
        // quoi que ce soit d'abord.
        _ = SafePath.Root;

        _bridge = BridgeHost.Start(BridgePort, _session, OnBridgeRequestAsync);
        await WaitForBridgeReadyAsync();
        await InitializeWebViewAsync();
    }

    /// <summary>
    /// Polls <c>/health</c> every 100 ms for up to 10 s so we don't
    /// navigate the main WebView2 to a URL that still 502s.
    /// </summary>
    private async Task WaitForBridgeReadyAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var r = await HealthClient.GetAsync($"http://127.0.0.1:{BridgePort}/health");
                if (r.IsSuccessStatusCode) return;
            }
            catch (HttpRequestException) { /* bridge not listening yet */ }
            catch (TaskCanceledException) { /* poll timeout, retry */ }
            await Task.Delay(100);
        }
    }

    /// <summary>
    /// Wires the main WebView2 to the bundled frontend assets, injects
    /// the Bearer token for the JS shim, and enables the web-message
    /// channel that lets the frontend request native dialogs.
    /// </summary>
    private async Task InitializeWebViewAsync()
    {
        await WebView.EnsureCoreWebView2Async();

        var frontendDir = FrontendAssets.Root;
        if (!Directory.Exists(frontendDir))
        {
            WebView.NavigateToString(
                "<!doctype html><html><body style='font-family:sans-serif;padding:40px;" +
                "background:#0b1220;color:#fff'>" +
                "<h1>Pinel</h1>" +
                "<p>Frontend assets missing at <code>" + frontendDir + "</code></p>" +
                "</body></html>");
            return;
        }

        WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "pinel.local",
            frontendDir,
            CoreWebView2HostResourceAccessKind.Allow);

        WebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

        if (_bridge is not null)
        {
            // Injected at document-creation time so app.js reads it synchronously.
            var tokenLiteral = JsonSerializer.Serialize(_bridge.Token);
            await WebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
                $"window.__PINEL_TOKEN = {tokenLiteral};");
        }

        WebView.CoreWebView2.Navigate("http://pinel.local/index.html");
    }

    /// <summary>
    /// Invoked by the bridge when an endpoint needs to delegate back to
    /// the UI process (currently just HTML → PDF rendering). The bridge
    /// runs on thread-pool threads; dispatch back to the UI thread.
    /// </summary>
    private async Task<object?> OnBridgeRequestAsync(BridgeRequest req)
    {
        if (req is BridgeRequest.HtmlToPdf html)
        {
            _pdfRenderer ??= new PdfRenderer(PdfWebView);
            var profile = html.Landscape ? PdfProfile.Dashboard : PdfProfile.Document;
            var output = await Dispatcher.InvokeAsync(async () =>
                await _pdfRenderer.RenderAsync(html.HtmlPath, html.OutputPath, profile));
            return new { output = await output };
        }
        return null;
    }

    /// <summary>
    /// Handles <c>postMessage</c> calls from the JS side. Supported types:
    /// <list type="bullet">
    ///   <item><c>selectFolder</c> - returns a folder path or <c>cancelled</c>.</item>
    ///   <item><c>selectFile</c> - with an optional <c>filter</c>.</item>
    /// </list>
    /// </summary>
    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string raw = e.WebMessageAsJson ?? e.TryGetWebMessageAsString() ?? "";
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var type = doc.RootElement.TryGetProperty("type", out var t) ? t.GetString() : null;
            var id = doc.RootElement.TryGetProperty("id", out var i) ? i.GetString() : null;

            switch (type)
            {
                case "selectFolder":
                    var folder = PickFolder();
                    RespondToFrontend(id, folder is null
                        ? new { cancelled = true }
                        : (object)new { folder });
                    break;

                case "selectFile":
                    var filter = doc.RootElement.TryGetProperty("filter", out var f) ? f.GetString() : null;
                    var file = PickFile(filter);
                    RespondToFrontend(id, file is null
                        ? new { cancelled = true }
                        : (object)new { file });
                    break;

                default:
                    RespondToFrontend(id, new { error = $"unknown type: {type}" });
                    break;
            }
        }
        catch (JsonException)
        {
            /* Malformed JSON from the page is ignored on purpose. */
        }
    }

    private void RespondToFrontend(string? id, object payload)
    {
        var json = JsonSerializer.Serialize(new { id, payload });
        Dispatcher.Invoke(() => WebView.CoreWebView2.PostWebMessageAsJson(json));
    }

    /// <summary>
    /// Shows the native Windows folder picker. Runs on the UI thread.
    /// </summary>
    private string? PickFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Sélectionner un dossier PMSI",
            InitialDirectory = SafePath.Root,
            Multiselect = false,
        };
        var result = Dispatcher.Invoke(() => dialog.ShowDialog(this));
        return result == true ? dialog.FolderName : null;
    }

    /// <summary>
    /// Shows the native Windows file picker. Runs on the UI thread.
    /// <paramref name="filter"/> uses the Win32 pipe-delimited syntax:
    /// <c>"CSV|*.csv|Tous les fichiers|*.*"</c>.
    /// </summary>
    private string? PickFile(string? filter)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Sélectionner un fichier",
            InitialDirectory = SafePath.Root,
            Filter = string.IsNullOrEmpty(filter) ? "Tous les fichiers|*.*" : filter,
            DereferenceLinks = true,
            CheckFileExists = true,
            Multiselect = false,
        };
        var result = Dispatcher.Invoke(() => dialog.ShowDialog(this));
        return result == true ? dialog.FileName : null;
    }

    /// <summary>
    /// Gracefully stops the ASP.NET Core bridge so port 8787 is released
    /// immediately, even if StopAsync hits the 2 s deadline.
    /// </summary>
    private async void OnClosed(object? sender, EventArgs e)
    {
        if (_bridge is not null)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await _bridge.App.StopAsync(cts.Token);
            }
            catch { /* best effort */ }
            finally
            {
                await _bridge.App.DisposeAsync();
            }
        }
    }
}

/// <summary>
/// Discriminated union of UI-thread callbacks the bridge can request.
/// Extend with more cases as new native features are exposed to the HTTP API.
/// </summary>
public abstract record BridgeRequest
{
    public sealed record HtmlToPdf(string HtmlPath, string OutputPath, bool Landscape) : BridgeRequest;
}
