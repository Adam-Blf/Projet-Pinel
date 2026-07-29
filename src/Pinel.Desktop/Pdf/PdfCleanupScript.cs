namespace Pinel.Desktop.Pdf;

/// <summary>
/// JS/CSS that is injected into the hidden WebView2 right before
/// <c>PrintToPdfAsync</c> to strip browser chrome, interactive widgets,
/// and layout artefacts that ruin the PDF output of BigQuery dashboards,
/// Looker Studio reports, and random web pages saved with Ctrl+S.
/// </summary>
public static class PdfCleanupScript
{
    /// <summary>
    /// Strip and normalize the DOM for a print-quality PDF.
    /// Preserves content, chart SVGs, and semantic text.
    /// </summary>
    public const string Dashboard = """
    (function(){
      try {
        // 1. Inject a print stylesheet that kills chrome, pagination, overflow.
        const style = document.createElement('style');
        style.textContent = `
          @page { margin: 12mm; }
          html, body {
            background: #fff !important;
            color: #111 !important;
            overflow: visible !important;
            height: auto !important;
            min-height: 0 !important;
            max-height: none !important;
            width: auto !important;
            zoom: 1 !important;
          }
          /* Kill Google/BigQuery/Looker chrome */
          header, nav, footer,
          [role="banner"], [role="navigation"],
          .header, .navbar, .nav, .menu, .sidebar, .toolbar,
          .cookie-banner, .consent-banner, .privacy-bar,
          #cookie, #consent, #onetrust-consent-sdk,
          .modal, .overlay, [role="dialog"],
          button, .btn, .button,
          [aria-label*="feedback" i], [aria-label*="help" i],
          [class*="FeedbackButton"], [class*="SupportButton"],
          [class*="resize-handle"], [class*="drag-handle"],
          iframe[src*="doubleclick"], iframe[src*="google-analytics"],
          script[src*="hotjar"], script[src*="intercom"] {
            display: none !important;
          }
          /* Remove sticky/fixed overlays that break pagination */
          *[style*="position: fixed"],
          *[style*="position:fixed"],
          [style*="position: sticky"],
          [style*="position:sticky"] {
            position: static !important;
            transform: none !important;
          }
          /* Expand scroll containers so content isn't clipped */
          *[style*="overflow: hidden"],
          *[style*="overflow:hidden"],
          *[style*="overflow: auto"],
          *[style*="overflow:auto"],
          *[style*="overflow: scroll"],
          *[style*="overflow:scroll"] {
            overflow: visible !important;
            max-height: none !important;
            height: auto !important;
          }
          /* Ensure charts render with their natural size */
          canvas, svg { max-width: 100% !important; height: auto !important; }
          /* Preserve exact Chart.js colors */
          * { -webkit-print-color-adjust: exact !important; print-color-adjust: exact !important; }
          /* Dark → light bridge: unreadable in print otherwise */
          [class*="dark"], .dark, html.dark, body.dark {
            background: #fff !important;
            color: #111 !important;
          }
          a { color: #000091 !important; text-decoration: underline; }
          /* Avoid page breaks inside important atoms */
          table, figure, .card, .widget, .chart, .tile { page-break-inside: avoid; break-inside: avoid; }
        `;
        document.head.appendChild(style);

        // 2. Strip common widget toolbars by tag/attribute walk.
        const killers = [
          'header', 'nav', 'footer',
          '[role="banner"]', '[role="navigation"]', '[role="dialog"]',
          'button', '.btn', '.button',
          '.cookie-banner', '.consent-banner',
          '.toast', '.snackbar', '.notification',
        ];
        for (const sel of killers) {
          document.querySelectorAll(sel).forEach(el => el.remove());
        }

        // 3. Force body width close to A4 landscape useable area.
        document.body.style.maxWidth = 'none';
        document.body.style.margin = '0';

        // 4. Lazy-loaded images: nudge them all into view so they render.
        for (const img of document.querySelectorAll('img[loading="lazy"]')) {
          img.loading = 'eager';
        }

        // 5. Remove animations that might not settle before print.
        const reduceMotion = document.createElement('style');
        reduceMotion.textContent = `
          *, *::before, *::after {
            animation-duration: 0s !important;
            animation-delay: 0s !important;
            transition-duration: 0s !important;
          }`;
        document.head.appendChild(reduceMotion);

        // 6. Signal readiness for the host.
        window.__SOVEREIGN_PDF_READY = true;
      } catch (e) {
        window.__SOVEREIGN_PDF_ERROR = String(e);
      }
    })();
    """;
}
