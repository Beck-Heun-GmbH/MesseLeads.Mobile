using System.Diagnostics;

namespace MesseLeads.Mobile.Services;

public sealed class AppStartupService
{
    private static readonly TimeSpan LookupRefreshInterval = TimeSpan.FromMinutes(5);

    private DateTime _lastLookupRefreshUtc = DateTime.MinValue;

    private readonly AuthSessionService _sessionService;
    private readonly INavigationService _navigationService;
    private readonly LookupSyncService _lookupSyncService;
    private readonly TradeFairSelectionService _tradeFairSelectionService;

    public AppStartupService(
        AuthSessionService sessionService,
        INavigationService navigationService,
        LookupSyncService lookupSyncService,
        TradeFairSelectionService tradeFairSelectionService)
    {
        _sessionService = sessionService;
        _navigationService = navigationService;
        _lookupSyncService = lookupSyncService;
        _tradeFairSelectionService = tradeFairSelectionService;
    }

    public async Task StartAsync()
    {
        var hasValidSession =
            await _sessionService.HasValidSessionAsync();

        if (!hasValidSession)
        {
            _sessionService.Logout();

            await _navigationService.GoToLoginAsync();
            return;
        }

        var mustChangePassword =
            await _sessionService.MustChangePasswordAsync();

        if (mustChangePassword)
        {
            await _navigationService.GoToChangePasswordAsync();
            return;
        }

        await ContinueAfterAuthenticationAsync();
    }

    public async Task ContinueAfterAuthenticationAsync()
    {
        if (!await _tradeFairSelectionService.HasSelectionAsync())
        {
            await _navigationService.GoToTradeFairSelectionAsync();
            return;
        }

        RefreshLookupsInBackground(force: true);
        await _navigationService.GoToHomeAsync();
    }

    /// <summary>
    /// Holt die Stammdaten neu vom Server. Beim App-Start erzwungen, bei jedem
    /// Zurückkehren in die App nur, wenn der letzte Abgleich länger her ist.
    /// </summary>
    public void RefreshLookupsInBackground(bool force = false)
    {
        _ = RefreshLookupsAsync(force);
    }

    public async Task RefreshLookupsOnResumeAsync()
    {
        if (!await _sessionService.HasValidSessionAsync() ||
            !await _tradeFairSelectionService.HasSelectionAsync())
        {
            return;
        }

        await RefreshLookupsAsync(force: false);
    }

    private async Task RefreshLookupsAsync(bool force)
    {
        if (!force && DateTime.UtcNow - _lastLookupRefreshUtc < LookupRefreshInterval)
        {
            return;
        }

        try
        {
            // Der Download ersetzt den lokalen Bestand vollständig, damit auf dem Server
            // gelöschte oder deaktivierte Stammdaten auch in der App verschwinden.
            var count = await _lookupSyncService.DownloadAsync();
            _lastLookupRefreshUtc = DateTime.UtcNow;
            Debug.WriteLine($"Stammdaten aktualisiert: {count}");
        }
        catch (Exception ex)
        {
            // Ohne Verbindung bleibt der zuletzt geladene Bestand erhalten.
            Debug.WriteLine("Automatischer Stammdaten-Download fehlgeschlagen: " + ex);
        }
    }
}
