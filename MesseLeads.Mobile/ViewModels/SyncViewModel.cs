using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MesseLeads.Mobile.Models;
using MesseLeads.Mobile.Services;

namespace MesseLeads.Mobile.ViewModels;

public partial class SyncViewModel : BaseViewModel
{
    private readonly LocalLeadService _localLeadService;
    private readonly LeadSyncService _leadSyncService;
    private readonly SyncHistoryService _syncHistoryService;

    [ObservableProperty]
    private string pendingText = "Wird geladen...";

    [ObservableProperty]
    private string resultText = "";

    [ObservableProperty]
    private bool hasHistory;

    [ObservableProperty]
    private bool hasNoHistory = true;

    public ObservableCollection<SyncHistoryListItem> History { get; } = [];

    public SyncViewModel(
        LocalLeadService localLeadService,
        LeadSyncService leadSyncService,
        SyncHistoryService syncHistoryService)
    {
        _localLeadService = localLeadService;
        _leadSyncService = leadSyncService;
        _syncHistoryService = syncHistoryService;
        Title = "Synchronisation";
    }

    public async Task LoadAsync()
    {
        var pending = await _localLeadService.CountPendingUploadsAsync();

        PendingText = pending == 0
            ? "Keine offenen Uploads."
            : $"{pending} Upload(s) warten.";

        await LoadHistoryAsync();
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
            ResultText = "Synchronisation läuft...";

            var result = await _leadSyncService.SyncPendingAsync("Manuell");

            ResultText = result.Message;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ResultText = "Fehler: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadHistoryAsync()
    {
        History.Clear();

        var entries = await _syncHistoryService.GetRecentAsync();

        foreach (var entry in entries)
        {
            History.Add(SyncHistoryListItem.From(entry));
        }

        HasHistory = History.Count > 0;
        HasNoHistory = !HasHistory;
    }
}

public sealed class SyncHistoryListItem
{
    public required string StatusText { get; init; }
    public required string TriggerText { get; init; }
    public required string TimeText { get; init; }
    public required string DetailText { get; init; }
    public required string Message { get; init; }
    public required Color AccentColor { get; init; }
    public required Color BackgroundColor { get; init; }

    public static SyncHistoryListItem From(LocalSyncHistory entry)
    {
        var localStarted = entry.StartedUtc.ToLocalTime();
        var duration = entry.FinishedUtc - entry.StartedUtc;
        var durationText = duration.TotalSeconds < 1
            ? "unter 1 s"
            : $"{duration.TotalSeconds:0} s";

        var detailParts = new List<string>
        {
            $"Leads: {entry.LeadSynced}/{entry.LeadTotal}",
            $"Bilder: {entry.ImageSynced}"
        };

        if (entry.LeadFailed > 0 || entry.ImageFailed > 0)
        {
            detailParts.Add($"Fehler: {entry.LeadFailed + entry.ImageFailed}");
        }

        return new SyncHistoryListItem
        {
            StatusText = entry.Success ? "Erfolgreich" : "Fehler",
            TriggerText = entry.Trigger,
            TimeText = $"{localStarted:dd.MM.yyyy HH:mm} · {durationText}",
            DetailText = string.Join(" · ", detailParts),
            Message = entry.Message,
            AccentColor = entry.Success
                ? Color.FromArgb("#188047")
                : Color.FromArgb("#A45B00"),
            BackgroundColor = entry.Success
                ? Color.FromArgb("#EAF8EF")
                : Color.FromArgb("#FFF7E2")
        };
    }
}
