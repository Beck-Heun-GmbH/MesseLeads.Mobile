#if ANDROID
using Android.Net.Http;
using Android.Webkit;

namespace MesseLeads.Mobile.Platforms.Android;

public class DebugWebViewClient : WebViewClient
{
    public override void OnReceivedSslError(
        global::Android.Webkit.WebView? view,
        SslErrorHandler? handler,
        SslError? error)
    {
        System.Diagnostics.Debug.WriteLine($"[SSL] Fehler: {error?.PrimaryError}");
        handler?.Proceed(); // SSL-Fehler ignorieren (NUR DEBUG!)
    }

    public override void OnPageFinished(
        global::Android.Webkit.WebView? view, string? url)
    {
        System.Diagnostics.Debug.WriteLine($"[WebView] Seite geladen: {url}");
        base.OnPageFinished(view, url);
    }
}
#endif