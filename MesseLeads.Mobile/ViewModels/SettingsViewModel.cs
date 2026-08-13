using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MesseLeads.Mobile.Models;
using MesseLeads.Mobile.Services;

namespace MesseLeads.Mobile.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly INavigationService _navigationService;
    private readonly LookupSyncService _lookupSyncService;
    private readonly AuthSessionService _authSessionService;
    private readonly TradeFairSelectionService _tradeFairSelectionService;

    [ObservableProperty]
    private string displayName = "Benutzer";

    [ObservableProperty]
    private string serverUrl = "https://ml.beck-heun.de:8888/";

    [ObservableProperty]
    private string appVersion = AppInfo.VersionString;

    [ObservableProperty]
    private string lookupStatusText = "Noch nicht geladen";

    [ObservableProperty]
    private bool hasLookupGroups;

    [ObservableProperty]
    private bool hasNoLookupGroups = true;

    [ObservableProperty]
    private string currentTradeFairText = "Keine Messe ausgewählt";

    [ObservableProperty]
    private string tradeFairStatusText = "";

    [ObservableProperty]
    private TradeFairSelection? selectedTradeFair;

    [ObservableProperty]
    private bool hasTradeFairs;

    public ObservableCollection<LookupGroupSummary> LookupGroups { get; } = [];
    public ObservableCollection<TradeFairSelection> TradeFairs { get; } = [];

    public SettingsViewModel(
        INavigationService navigationService,
        LookupSyncService lookupSyncService,
        AuthSessionService authSessionService,
        TradeFairSelectionService tradeFairSelectionService)
    {
        _navigationService = navigationService;
        _lookupSyncService = lookupSyncService;
        _authSessionService = authSessionService;
        _tradeFairSelectionService = tradeFairSelectionService;
        Title = "Einstellungen";
    }

    public async Task LoadAsync()
    {
        DisplayName = await _authSessionService.GetDisplayNameAsync();

        var count = await _lookupSyncService.CountAsync();
        LookupStatusText = count == 0
            ? "Noch nicht geladen"
            : $"{count} Stammdaten lokal gespeichert";

        await LoadLookupGroupsAsync();

        try
        {
            await LoadTradeFairsAsync();
        }
        catch (Exception ex)
        {
            TradeFairStatusText = "Messen konnten nicht geladen werden: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task DownloadLookupsAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            LookupStatusText = "Stammdaten werden geladen...";

            var count = await _lookupSyncService.DownloadAsync();

            LookupStatusText = $"{count} Stammdaten heruntergeladen";
            await LoadLookupGroupsAsync();
            await LoadTradeFairsAsync(useServerRefresh: false);
        }
        catch (Exception ex)
        {
            LookupStatusText = "Fehler beim Laden: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task LogoutAsync()
    {
        _authSessionService.Logout();
        return _navigationService.GoToLoginAsync();
    }

    private async Task LoadLookupGroupsAsync()
    {
        LookupGroups.Clear();

        var items = await _lookupSyncService.GetAllAsync();

        foreach (var group in items
                     .GroupBy(x => x.Group)
                     .OrderBy(x => x.Key))
        {
            LookupGroups.Add(new LookupGroupSummary
            {
                Group = group.Key,
                Count = group.Count()
            });
        }

        HasLookupGroups = LookupGroups.Count > 0;
        HasNoLookupGroups = !HasLookupGroups;
    }

    [RelayCommand]
    private async Task RefreshTradeFairsAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            TradeFairStatusText = "Messen werden geladen...";
            await LoadTradeFairsAsync(useServerRefresh: true);
        }
        catch (Exception ex)
        {
            TradeFairStatusText = "Messen konnten nicht geladen werden: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveTradeFairAsync()
    {
        if (SelectedTradeFair is null)
        {
            TradeFairStatusText = "Bitte eine Messe auswählen.";
            return;
        }

        await _tradeFairSelectionService.SaveSelectionAsync(SelectedTradeFair);
        CurrentTradeFairText = SelectedTradeFair.Label;
        TradeFairStatusText = "Aktive Messe gespeichert. Neue Leads werden dieser Messe zugeordnet.";
    }

    private async Task LoadTradeFairsAsync(bool useServerRefresh = false)
    {
        var currentSelection = await _tradeFairSelectionService.GetSelectedAsync();
        CurrentTradeFairText = currentSelection?.Label ?? "Keine Messe ausgewählt";

        List<TradeFairSelection> tradeFairs;

        if (useServerRefresh)
        {
            tradeFairs = await _tradeFairSelectionService.DownloadTradeFairsAsync();
        }
        else
        {
            tradeFairs = await _tradeFairSelectionService.GetLocalTradeFairsAsync();
            if (tradeFairs.Count == 0)
            {
                tradeFairs = await _tradeFairSelectionService.EnsureTradeFairsAvailableAsync();
            }
        }

        TradeFairs.Clear();

        foreach (var tradeFair in tradeFairs)
        {
            TradeFairs.Add(tradeFair);
        }

        HasTradeFairs = TradeFairs.Count > 0;
        SelectedTradeFair =
            TradeFairs.FirstOrDefault(x => x.Key == currentSelection?.Key) ??
            TradeFairs.FirstOrDefault();

        TradeFairStatusText = TradeFairs.Count == 0
            ? "Keine aktive Messe verfügbar."
            : "Wähle eine Messe und speichere sie für neue Leads.";
    }
}
