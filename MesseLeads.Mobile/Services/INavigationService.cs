using MesseLeads.Mobile.Models;

namespace MesseLeads.Mobile.Services;

public interface INavigationService
{
    Task GoToHomeAsync();
    Task GoToLoginAsync();
    Task GoToChangePasswordAsync();
    Task GoToTradeFairSelectionAsync();
    Task GoToNewLeadStartAsync();
    Task GoToDraftsAsync();
    Task GoToHistoryAsync();
    Task GoToSyncAsync();
    Task GoToSettingsAsync();
    Task GoToLeadWizardAsync(Guid? localLeadId = null, LeadStartMode startMode = LeadStartMode.Photo);
    Task GoBackAsync();
}
