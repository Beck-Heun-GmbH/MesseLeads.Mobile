using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MesseLeads.Mobile.Models;
using MesseLeads.Mobile.Services;

namespace MesseLeads.Mobile.ViewModels;

public partial class TradeFairSelectionViewModel : BaseViewModel
{
    private readonly TradeFairSelectionService _tradeFairSelectionService;
    private readonly LookupSyncService _lookupSyncService;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private TradeFairSelection? selectedTradeFair;

    [ObservableProperty]
    private string statusText = "Messen werden geladen...";

    [ObservableProperty]
    private bool hasTradeFairs;

    [ObservableProperty]
    private bool hasNoTradeFairs = true;

    [ObservableProperty]
    private bool canConfirm;

    public ObservableCollection<TradeFairSelection> TradeFairs { get; } = [];

    public TradeFairSelectionViewModel(
        TradeFairSelectionService tradeFairSelectionService,
        LookupSyncService lookupSyncService,
        INavigationService navigationService)
    {
        _tradeFairSelectionService = tradeFairSelectionService;
        _lookupSyncService = lookupSyncService;
        _navigationService = navigationService;
        Title = "Messe auswählen";
    }

    partial void OnSelectedTradeFairChanged(TradeFairSelection? value)
    {
        CanConfirm = value is not null;
    }

    public async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusText = "Messen werden geladen...";

            var tradeFairs = await _tradeFairSelectionService.DownloadTradeFairsAsync();
            var currentSelection = await _tradeFairSelectionService.GetSelectedAsync();
            ApplyTradeFairs(tradeFairs, currentSelection);
            StatusText = tradeFairs.Count == 0
                ? "Der Server hat keine aktive Messe geliefert."
                : "Bitte wähle die Messe aus, auf der du Leads erfassen möchtest.";
        }
        catch (Exception ex)
        {
            var localTradeFairs = await _tradeFairSelectionService.GetLocalTradeFairsAsync();
            var currentSelection = await _tradeFairSelectionService.GetSelectedAsync();
            ApplyTradeFairs(localTradeFairs, currentSelection);
            StatusText = localTradeFairs.Count == 0
                ? "Messen konnten nicht geladen werden: " + ex.Message
                : "Offline-Modus: lokal gespeicherte Messen werden angezeigt.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task RefreshAsync()
    {
        return LoadAsync();
    }

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        if (SelectedTradeFair is null)
        {
            StatusText = "Bitte zuerst eine Messe auswählen.";
            return;
        }

        await _tradeFairSelectionService.SaveSelectionAsync(SelectedTradeFair);
        _ = RefreshLookupsInBackgroundAsync();
        await _navigationService.GoToHomeAsync();
    }

    private async Task RefreshLookupsInBackgroundAsync()
    {
        try
        {
            await _lookupSyncService.DownloadAsync();
        }
        catch
        {
            // Ohne Verbindung wird beim nächsten App-Start erneut versucht.
        }
    }

    private void ApplyTradeFairs(
        IReadOnlyList<TradeFairSelection> tradeFairs,
        TradeFairSelection? currentSelection)
    {
        TradeFairs.Clear();

        foreach (var tradeFair in tradeFairs)
        {
            TradeFairs.Add(tradeFair);
        }

        HasTradeFairs = TradeFairs.Count > 0;
        HasNoTradeFairs = !HasTradeFairs;
        SelectedTradeFair =
            TradeFairs.FirstOrDefault(x => x.Key == currentSelection?.Key) ??
            TradeFairs.FirstOrDefault();
    }
}
