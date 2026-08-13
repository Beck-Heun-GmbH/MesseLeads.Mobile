using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MesseLeads.Mobile.Models;
using MesseLeads.Mobile.Services;

namespace MesseLeads.Mobile.ViewModels;

public partial class DraftsViewModel : BaseViewModel
{
    private readonly LocalLeadService _localLeadService;
    private readonly INavigationService _navigationService;

    public ObservableCollection<DraftListItem> Drafts { get; } = [];

    [ObservableProperty]
    private bool hasDrafts;

    [ObservableProperty]
    private string emptyText = "Noch keine Entwürfe vorhanden.";

    public DraftsViewModel(
        LocalLeadService localLeadService,
        INavigationService navigationService)
    {
        _localLeadService = localLeadService;
        _navigationService = navigationService;
        Title = "Entwürfe";
    }

    public async Task LoadAsync()
    {
        IsBusy = true;

        try
        {
            Drafts.Clear();

            var leads = await _localLeadService.GetDraftsAsync();

            foreach (var lead in leads)
            {
                Drafts.Add(new DraftListItem
                {
                    LocalId = lead.LocalId,
                    Title = BuildTitle(lead),
                    Subtitle = BuildSubtitle(lead),
                    StatusText = BuildStatusText(lead.SyncStatus),
                    UpdatedText = $"Geändert: {lead.UpdatedUtc.ToLocalTime():dd.MM.yyyy HH:mm}"
                });
            }

            HasDrafts = Drafts.Count > 0;
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

        await _navigationService.GoToLeadWizardAsync(item.LocalId);
    }

    [RelayCommand]
    private async Task DeleteAsync(DraftListItem? item)
    {
        if (item is null)
        {
            return;
        }

        var lead = await _localLeadService.GetAsync(item.LocalId);

        if (lead is null)
        {
            return;
        }

        await _localLeadService.DeleteAsync(lead);
        await LoadAsync();
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

        return parts.Count == 0 ? "Noch keine Kontaktdaten" : string.Join(" · ", parts);
    }

    private static string BuildStatusText(string status)
    {
        return status switch
        {
            "Draft" => "Entwurf",
            "PendingUpload" => "Wartet auf Upload",
            "PendingUpdate" => "Änderung wartet",
            "Synced" => "Synchronisiert",
            _ => status
        };
    }
}