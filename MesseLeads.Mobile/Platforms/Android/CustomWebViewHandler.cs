#if ANDROID
using Android.OS;
using Microsoft.Maui.Handlers;

namespace MesseLeads.Mobile.Platforms.Android;

public class CustomWebViewHandler : WebViewHandler
{
    protected override void ConnectHandler(global::Android.Webkit.WebView platformView)
    {
        base.ConnectHandler(platformView);

        platformView.Settings.JavaScriptEnabled = true;
        platformView.Settings.DomStorageEnabled = true;
        platformView.Settings.AllowFileAccess = true;
        platformView.Settings.AllowContentAccess = true;
        platformView.Settings.MediaPlaybackRequiresUserGesture = false;
        platformView.Settings.SetSupportMultipleWindows(true);

        // Verzögert setzen, damit MAUI seinen internen Client zuerst setzt
        // und wir danach überschreiben
        var handler = new Handler(Looper.MainLooper!);
        handler.Post(() =>
        {
            platformView.SetWebChromeClient(new CustomWebChromeClient());
            platformView.SetWebViewClient(new DebugWebViewClient());
            System.Diagnostics.Debug.WriteLine("[WebView] CustomWebChromeClient gesetzt");
        });
    }
}
#endif