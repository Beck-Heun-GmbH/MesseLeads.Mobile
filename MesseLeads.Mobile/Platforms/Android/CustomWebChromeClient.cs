#if ANDROID

using Android.Content;
using Android.Net;
using Android.Provider;
using Android.Webkit;
using Microsoft.Maui.ApplicationModel;
using AUri = Android.Net.Uri;

namespace MesseLeads.Mobile.Platforms.Android;

public sealed class CustomWebChromeClient : WebChromeClient
{
    private static IValueCallback? _filePathCallback;
    private static AUri? _cameraImageUri;

    public override bool OnShowFileChooser(
    global::Android.Webkit.WebView? webView,
    IValueCallback? filePathCallback,
    FileChooserParams? fileChooserParams)
    {
        _filePathCallback?.OnReceiveValue(null);
        _filePathCallback = filePathCallback;

        var acceptTypes = fileChooserParams?.GetAcceptTypes();
        bool isCaptureEnabled = fileChooserParams?.IsCaptureEnabled ?? false;

        // Temporäres Logging
        System.Diagnostics.Debug.WriteLine($"[Camera] IsCaptureEnabled: {isCaptureEnabled}");
        System.Diagnostics.Debug.WriteLine($"[Camera] AcceptTypes: {string.Join(", ", acceptTypes ?? [])}");
        System.Diagnostics.Debug.WriteLine($"[Camera] Mode: {fileChooserParams?.Mode}");

        if (isCaptureEnabled)
        {
            _ = OpenCameraAsync();
        }
        else
        {
            var activity = Platform.CurrentActivity;
            if (activity is null)
            {
                _filePathCallback?.OnReceiveValue(null);
                _filePathCallback = null;
                return true;
            }

            var galleryIntent = new Intent(Intent.ActionGetContent);
            galleryIntent.SetType("image/*");
            var chooser = Intent.CreateChooser(galleryIntent, "Bild auswählen");
            activity.StartActivityForResult(chooser, MainActivity.CameraRequestCode);
        }

        return true;
    }

    private static async Task OpenCameraAsync()
    {
        try
        {
            var permissionStatus = await Permissions.CheckStatusAsync<Permissions.Camera>();

            if (permissionStatus != PermissionStatus.Granted)
            {
                permissionStatus = await Permissions.RequestAsync<Permissions.Camera>();
            }

            if (permissionStatus != PermissionStatus.Granted)
            {
                _filePathCallback?.OnReceiveValue(null);
                _filePathCallback = null;
                return;
            }

            var activity = Platform.CurrentActivity;
            if (activity is null)
            {
                _filePathCallback?.OnReceiveValue(null);
                _filePathCallback = null;
                return;
            }

            var picturesDirectory = activity.GetExternalFilesDir(global::Android.OS.Environment.DirectoryPictures);
            var fileName = $"messelead_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
            var imageFile = new Java.IO.File(picturesDirectory, fileName);

            _cameraImageUri = FileProvider.GetUriForFile(
                activity,
                $"{activity.PackageName}.fileprovider",
                imageFile);

            var cameraIntent = new Intent(MediaStore.ActionImageCapture);
            cameraIntent.PutExtra(MediaStore.ExtraOutput, _cameraImageUri);
            cameraIntent.AddFlags(ActivityFlags.GrantReadUriPermission);
            cameraIntent.AddFlags(ActivityFlags.GrantWriteUriPermission);

            activity.StartActivityForResult(cameraIntent, MainActivity.CameraRequestCode);
        }
        catch
        {
            _filePathCallback?.OnReceiveValue(null);
            _filePathCallback = null;
        }
    }

    public static void HandleCameraResult(
    global::Android.App.Result resultCode,
    global::Android.Content.Intent? data)
    {
        if (_filePathCallback is null) return;

        if (resultCode == global::Android.App.Result.Ok)
        {
            // Galerie-Auswahl hat eine URI im Intent
            var uri = data?.Data ?? _cameraImageUri;

            if (uri is not null)
                _filePathCallback.OnReceiveValue(new AUri[] { uri });
            else
                _filePathCallback.OnReceiveValue(null);
        }
        else
        {
            _filePathCallback.OnReceiveValue(null);
        }

        _filePathCallback = null;
        _cameraImageUri = null;
    }
}

#endif