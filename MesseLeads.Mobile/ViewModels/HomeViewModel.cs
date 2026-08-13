using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MesseLeads.Mobile.Services;
using MesseLeads.Mobile.Models;

namespace MesseLeads.Mobile.ViewModels;

public partial class HomeViewModel : BaseViewModel
{
    private readonly INavigationService _navigationService;
    private readonly LocalLeadService _localLeadService;
    private readonly LeadSyncService _leadSyncService;
    private readonly AuthSessionService _authSessionService;
    private readonly TradeFairSelectionService _tradeFairSelectionService;

    [ObservableProperty]
    private string displayName = "Benutzer";

    [ObservableProperty]
    private string syncStatusText = "Synchronisiert";

    [ObservableProperty]
    private string pendingUploadsText = "0 offene Uploads";

    [ObservableProperty]
    private string draftsText = "Noch keine Entwürfe";

    [ObservableProperty]
    private string lastSyncText = "";

    [ObservableProperty]
    private string syncButtonText = "Synchronisieren";

    [ObservableProperty]
    private string tradeFairHeaderText = "MesseLeads · Messe auswählen";

    [ObservableProperty]
    private bool showSaveBanner;

    [ObservableProperty]
    private bool showSaveBannerSynced;

    [ObservableProperty]
    private bool showSaveBannerLocalOnly;

    [ObservableProperty]
    private bool showSaveBannerSyncing;

    [ObservableProperty]
    private string saveBannerTitle = "";

    [ObservableProperty]
    private string saveBannerMessage = "";

    private CancellationTokenSource? _saveBannerCts;

    public HomeViewModel(
        INavigationService navigationService,
        LocalLeadService localLeadService,
        LeadSyncService leadSyncService,
        AuthSessionService authSessionService,
        TradeFairSelectionService tradeFairSelectionService)
    {
        _navigationService = navigationService;
        _localLeadService = localLeadService;
        _leadSyncService = leadSyncService;
        _authSessionService = authSessionService;
        _tradeFairSelectionService = tradeFairSelectionService;
        Title = "MesseLeads";
        LeadSaveNotificationState.Published += OnSaveNotificationPublished;
    }

    public async Task LoadAsync()
    {
        DisplayName = await _authSessionService.GetDisplayNameAsync();
        var tradeFair = await _tradeFairSelectionService.GetSelectedLabelOrFallbackAsync();
        TradeFairHeaderText = $"MesseLeads · {tradeFair}";

        var drafts = await _localLeadService.CountDraftsAsync();
        var pendingUploads = await _localLeadService.CountPendingUploadsAsync();

        DraftsText = drafts == 0
            ? "Noch keine lokalen Leads"
            : $"{drafts} lokale Lead(s)";

        PendingUploadsText = pendingUploads == 0
            ? "Keine offenen Uploads"
            : $"{pendingUploads} Upload(s) warten";

        SyncStatusText = pendingUploads == 0
            ? "Synchronisiert"
            : "Uploads offen";

        var lastSync = await _authSessionService.GetValueAsync("last_sync_utc");

        LastSyncText = string.IsNullOrWhiteSpace(lastSync)
            ? "Noch keine Synchronisation"
            : $"Letzter Sync: {DateTime.Parse(lastSync).ToLocalTime():dd.MM.yyyy HH:mm}";

        ApplyPendingSaveNotification();
    }

    [RelayCommand]
    private Task CaptureBusinessCardAsync()
    {
        return _navigationService.GoToLeadWizardAsync(startMode: LeadStartMode.Photo);
    }

    [RelayCommand]
    private Task PickBusinessCardPhotoAsync()
    {
        return _navigationService.GoToLeadWizardAsync(startMode: LeadStartMode.Gallery);
    }

    [RelayCommand]
    private Task ScanQrCodeAsync()
    {
        return _navigationService.GoToLeadWizardAsync(startMode: LeadStartMode.Qr);
    }

    [RelayCommand]
    private Task CreateManualLeadAsync()
    {
        return _navigationService.GoToLeadWizardAsync(startMode: LeadStartMode.Manual);
    }

    [RelayCommand]
    private Task NewLeadAsync()
    {
        return _navigationService.GoToNewLeadStartAsync();
    }

    [RelayCommand]
    private Task DraftsAsync()
    {
        return _navigationService.GoToDraftsAsync();
    }

    [RelayCommand]
    private Task HistoryAsync()
    {
        return _navigationService.GoToHistoryAsync();
    }

    [RelayCommand]
    private async Task SyncAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            SyncButtonText = "Synchronisiere...";

            var result = await _leadSyncService.SyncPendingAsync();

            if (result.Success)
            {
                await SecureStorage.SetAsync("last_sync_utc", DateTime.UtcNow.ToString("O"));
            }

            await LoadAsync();
        }
        finally
        {
            SyncButtonText = "Synchronisieren";
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task SettingsAsync()
    {
        return _navigationService.GoToSettingsAsync();
    }

    [RelayCommand]
    private Task OpenSyncAsync()
    {
        return _navigationService.GoToSyncAsync();
    }

    [RelayCommand]
    private async Task OpenMenuAsync()
    {
        var action = await Shell.Current.DisplayActionSheet(
            "MesseLeads",
            "Abbrechen",
            null,
            "Entwürfe öffnen",
            "Verlauf öffnen",
            "Synchronisieren",
            "Einstellungen");

        switch (action)
        {
            case "Entwürfe öffnen":
                await DraftsAsync();
                break;

            case "Verlauf öffnen":
                await HistoryAsync();
                break;

            case "Synchronisieren":
                await SyncAsync();
                break;

            case "Einstellungen":
                await SettingsAsync();
                break;
        }
    }

    private void ApplyPendingSaveNotification()
    {
        var notification = LeadSaveNotificationState.Consume();
        if (notification is null)
        {
            return;
        }

        ShowSaveNotification(notification);
    }

    private void OnSaveNotificationPublished(LeadSaveNotification notification)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ShowSaveNotification(notification);
            _ = LoadAsync();
        });
    }

    private void ShowSaveNotification(LeadSaveNotification notification)
    {
        ShowSaveBannerSyncing = notification.Kind == LeadSaveNotificationKind.Syncing;
        ShowSaveBannerSynced = notification.Kind == LeadSaveNotificationKind.Synced;
        ShowSaveBannerLocalOnly = notification.Kind is LeadSaveNotificationKind.LocalOnly or LeadSaveNotificationKind.PartiallySynced;
        ShowSaveBanner = true;

        SaveBannerTitle = notification.Kind switch
        {
            LeadSaveNotificationKind.Synced => "Lead gespeichert und synchronisiert",
            LeadSaveNotificationKind.Syncing => "Lead gespeichert",
            LeadSaveNotificationKind.PartiallySynced => "Lead synchronisiert, Foto wartet",
            _ => "Lead lokal gespeichert"
        };

        SaveBannerMessage = notification.Kind switch
        {
            LeadSaveNotificationKind.Synced => "Die Daten sind auf dem Server angekommen.",
            LeadSaveNotificationKind.Syncing => "Die Synchronisation läuft im Hintergrund.",
            LeadSaveNotificationKind.PartiallySynced => "Die Kontaktdaten sind gespeichert. Das Foto wird beim nächsten Sync erneut übertragen.",
            _ => "Keine Verbindung erkannt. Die Synchronisation wird später nachgeholt."
        };

        _saveBannerCts?.Cancel();
        _saveBannerCts = new CancellationTokenSource();
        _ = HideSaveBannerAfterDelayAsync(_saveBannerCts.Token);
    }

    private async Task HideSaveBannerAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(6), cancellationToken);
            ShowSaveBanner = false;
            ShowSaveBannerSyncing = false;
            ShowSaveBannerSynced = false;
            ShowSaveBannerLocalOnly = false;
        }
        catch (TaskCanceledException)
        {
        }
    }
}
