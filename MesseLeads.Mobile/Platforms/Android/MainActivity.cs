using Android.App;
using Android.Content.PM;
using Android.Content;
using Android.OS;

namespace MesseLeads.Mobile;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges =
        ConfigChanges.ScreenSize |
        ConfigChanges.Orientation |
        ConfigChanges.UiMode |
        ConfigChanges.ScreenLayout |
        ConfigChanges.SmallestScreenSize |
        ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    public const int CameraRequestCode = 9101;

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);

        if (requestCode == CameraRequestCode)
        {
            Platforms.Android.CustomWebChromeClient.HandleCameraResult(resultCode, data);
        }
    }
}