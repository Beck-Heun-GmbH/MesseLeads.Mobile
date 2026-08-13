using MesseLeads.Mobile.Models;

namespace MesseLeads.Mobile.Services;

public sealed class AppNavigationService : INavigationService
{
    public Task GoToHomeAsync() => Shell.Current.GoToAsync("//home");
    public Task GoToLoginAsync() => Shell.Current.GoToAsync("//login");
    public Task GoToTradeFairSelectionAsync() => Shell.Current.GoToAsync("//trade-fair-selection");
    public Task GoToNewLeadStartAsync() => Shell.Current.GoToAsync("new-lead-start");
    public Task GoToDraftsAsync() => Shell.Current.GoToAsync("drafts");
    public Task GoToHistoryAsync() => Shell.Current.GoToAsync("history");
    public Task GoToSyncAsync() => Shell.Current.GoToAsync("sync");
    public Task GoToSettingsAsync() => Shell.Current.GoToAsync("settings");

    public Task GoToLeadWizardAsync(Guid? localLeadId = null, LeadStartMode startMode = LeadStartMode.Photo)
    {
        LeadWizardRouteState.LocalLeadId = localLeadId;
        LeadWizardRouteState.StartMode = startMode;

        return Shell.Current.GoToAsync("lead-wizard");
    }

    public Task GoToChangePasswordAsync()
    {
        return Shell.Current.GoToAsync("//change-password");
    }

    public Task GoBackAsync() => Shell.Current.GoToAsync("..");
}

public static class LeadWizardRouteState
{
    public static Guid? LocalLeadId { get; set; }
    public static LeadStartMode StartMode { get; set; } = LeadStartMode.Photo;
}
