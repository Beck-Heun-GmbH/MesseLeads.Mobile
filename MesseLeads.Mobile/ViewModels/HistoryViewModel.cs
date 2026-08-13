using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MesseLeads.Mobile.Models;
using MesseLeads.Mobile.Services;

namespace MesseLeads.Mobile.ViewModels;

public partial class HistoryViewModel : BaseViewModel
{
    private readonly LocalLeadService _localLeadService;
    private readonly INavigationService _navigationService;

    public ObservableCollection<DraftListItem> Leads { get; } = [];

    [ObservableProperty]
    private bool hasLeads;

    [ObservableProperty]
    private string emptyText = "Noch keine fertigen Leads vorhanden.";

    public HistoryViewModel(
        LocalLeadService localLeadService,
        INavigationService navigationService)
    {
        _localLeadService = localLeadService;
        _navigationService = navigationService;
        Title = "Verlauf";
    }

    public async Task LoadAsync()
    {
        IsBusy = true;

        try
        {
            Leads.Clear();

            var leads = await _localLeadService.GetHistoryAsync();

            foreach (var lead in leads)
            {
                Leads.Add(new DraftListItem
                {
                    LocalId = lead.LocalId,
                    Title = BuildTitle(lead),
                    Subtitle = BuildSubtitle(lead),
                    StatusText = BuildStatusText(lead.SyncStatus),
                    UpdatedText = BuildSyncText(lead)
                });
            }

            HasLeads = Leads.Count > 0;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenAsync(DraftListItem? item)
    {
        if (item is null)
        {
            return;
        }

        await _navigationService.GoToLeadWizardAsync(item.LocalId, LeadStartMode.Manual);
    }

    private static string BuildTitle(LocalLead lead)
    {
        if (!string.IsNullOrWhiteSpace(lead.Company))
        {
            return lead.Company;
        }

        var name = $"{lead.FirstName} {lead.LastName}".Trim();

        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        return "Unbenannter Lead";
    }

    private static string BuildSubtitle(LocalLead lead)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(lead.FirstName) || !string.IsNullOrWhiteSpace(lead.LastName))
        {
            parts.Add($"{lead.FirstName} {lead.LastName}".Trim());
        }

        if (!string.IsNullOrWhiteSpace(lead.Email))
        {
            parts.Add(lead.Email);
        }

        if (!string.IsNullOrWhiteSpace(lead.TradeFair))
        {
            parts.Add(lead.TradeFair);
        }

        return parts.Count == 0 ? "Keine Kontaktdaten" : string.Join(" · ", parts);
    }

    private static string BuildStatusText(string status)
    {
        return status switch
        {
            "PendingUpdate" => "Änderung wartet auf Upload",
            "Synced" => "Synchronisiert",
            _ => status
        };
    }

    private static string BuildSyncText(LocalLead lead)
    {
        if (lead.SyncStatus == "PendingUpdate")
        {
            return $"Geändert: {lead.UpdatedUtc.ToLocalTime():dd.MM.yyyy HH:mm}";
        }

        return lead.LastSyncedUtc is null
            ? $"Erfasst: {lead.CreatedUtc.ToLocalTime():dd.MM.yyyy HH:mm}"
            : $"Übertragen: {lead.LastSyncedUtc.Value.ToLocalTime():dd.MM.yyyy HH:mm}";
    }
}
