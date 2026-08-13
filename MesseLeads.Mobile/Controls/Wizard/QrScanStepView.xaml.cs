using MesseLeads.Mobile.ViewModels;
using ZXing.Net.Maui;

namespace MesseLeads.Mobile.Controls.Wizard;

public partial class QrScanStepView : ContentView
{
    private int _isHandlingScan;

    public QrScanStepView()
    {
        InitializeComponent();

        CameraView.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormat.QrCode,
            AutoRotate = true,
            Multiple = false,
            TryHarder = true
        };
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        if (BindingContext is not LeadWizardViewModel viewModel)
        {
            return;
        }

        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.Camera>();
            }

            viewModel.SetQrPermissionState(status == PermissionStatus.Granted);
        }
        catch (Exception ex)
        {
            viewModel.SetQrPermissionState(false);
            await Shell.Current.DisplayAlert(
                "Kamera nicht verfügbar",
                "Der Kamerazugriff konnte nicht gestartet werden: " + ex.Message,
                "OK");
        }
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        CameraView.IsDetecting = false;
        Interlocked.Exchange(ref _isHandlingScan, 0);
    }

    private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        var value = e.Results?.FirstOrDefault()?.Value;

        if (string.IsNullOrWhiteSpace(value) ||
            Interlocked.Exchange(ref _isHandlingScan, 1) == 1)
        {
            return;
        }

        CameraView.IsDetecting = false;

        Dispatcher.Dispatch(async () =>
        {
            try
            {
                if (BindingContext is LeadWizardViewModel viewModel)
                {
                    await viewModel.AcceptQrCodeAsync(value);
                }
            }
            finally
            {
                Interlocked.Exchange(ref _isHandlingScan, 0);
            }
        });
    }
}
