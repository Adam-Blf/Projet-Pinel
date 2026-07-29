using System.IO;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Pinel.Desktop.Pdf;

/// <summary>
/// Converts an HTML document into a high-fidelity, print-ready PDF by
/// loading it inside a hidden WebView2 control and calling
/// <c>CoreWebView2.PrintToPdfAsync</c>. No external dependencies: the
/// renderer reuses the Chromium engine that ships with the
/// <see cref="Microsoft.Web.WebView2"/> package.
/// </summary>
/// <remarks>
/// Two output profiles are supported:
/// <list type="bullet">
///   <item><see cref="PdfProfile.Document"/> - portrait A4, light cleanup, used for the built-in manual.</item>
///   <item><see cref="PdfProfile.Dashboard"/> - landscape, aggressive DOM cleanup, used for BigQuery / Looker Studio dashboards saved as <c>.html</c>.</item>
/// </list>
/// The "dashboard" cleanup is defined in <see cref="PdfCleanupScript.Dashboard"/>
/// and is injected after the page loads but before <c>PrintToPdfAsync</c>.
/// </remarks>
public sealed class PdfRenderer
{
    private readonly WebView2 _renderer;

    public PdfRenderer(WebView2 hiddenWebView)
    {
        _renderer = hiddenWebView;
    }

    /// <summary>
    /// Loads <paramref name="htmlPath"/>, applies the cleanup script for
    /// the chosen <paramref name="profile"/>, and writes the PDF to
    /// <paramref name="outputPath"/>. Returns the output path.
    /// </summary>
    public async Task<string> RenderAsync(
        string htmlPath,
        string outputPath,
        PdfProfile profile = PdfProfile.Document)
    {
        if (!File.Exists(htmlPath))
        {
            throw new FileNotFoundException($"HTML file not found: {htmlPath}", htmlPath);
        }

        await _renderer.EnsureCoreWebView2Async();

        var uri = new Uri(htmlPath).AbsoluteUri;
        var ready = new TaskCompletionSource<bool>();

        void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            _renderer.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
            ready.TrySetResult(e.IsSuccess);
        }

        _renderer.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
        _renderer.CoreWebView2.Navigate(uri);

        if (!await ready.Task.WaitAsync(TimeSpan.FromSeconds(30)))
        {
            throw new InvalidOperationException("HTML navigation failed or timed out");
        }

        // Give the page time to run its own scripts (Chart.js render, web fonts).
        await Task.Delay(600);

        // Aggressive DOM cleanup for dashboards, light touch for documents.
        if (profile == PdfProfile.Dashboard)
        {
            await _renderer.CoreWebView2.ExecuteScriptAsync(PdfCleanupScript.Dashboard);
            // Let the cleanup settle (element removal + layout reflow).
            await Task.Delay(300);
        }

        var settings = _renderer.CoreWebView2.Environment.CreatePrintSettings();
        settings.Orientation = profile == PdfProfile.Dashboard
            ? CoreWebView2PrintOrientation.Landscape
            : CoreWebView2PrintOrientation.Portrait;
        settings.ShouldPrintBackgrounds = true;
        settings.ShouldPrintHeaderAndFooter = false;
        settings.MarginTop = 12.0;
        settings.MarginBottom = 12.0;
        settings.MarginLeft = 12.0;
        settings.MarginRight = 12.0;
        // Scale content to fit the page width. 0.85 keeps large dashboards readable.
        settings.ScaleFactor = profile == PdfProfile.Dashboard ? 0.80 : 1.0;

        var ok = await _renderer.CoreWebView2.PrintToPdfAsync(outputPath, settings);
        if (!ok)
        {
            throw new InvalidOperationException("PrintToPdfAsync returned false");
        }
        return outputPath;
    }
}

/// <summary>
/// Selects the rendering preset used by <see cref="PdfRenderer"/>.
/// </summary>
public enum PdfProfile
{
    /// <summary>Portrait A4, no DOM cleanup. Used for the built-in manual and static docs.</summary>
    Document,
    /// <summary>Landscape A4, aggressive cleanup. Used for BigQuery / Looker Studio / any web-saved dashboard.</summary>
    Dashboard,
}
