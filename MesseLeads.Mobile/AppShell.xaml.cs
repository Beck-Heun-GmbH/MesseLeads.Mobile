using MesseLeads.Mobile.Views;

namespace MesseLeads.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute("new-lead-start", typeof(NewLeadStartPage));
        Routing.RegisterRoute("lead-wizard", typeof(LeadWizardPage));
        Routing.RegisterRoute("drafts", typeof(DraftsPage));
        Routing.RegisterRoute("history", typeof(HistoryPage));
        Routing.RegisterRoute("sync", typeof(SyncPage));
        Routing.RegisterRoute("settings", typeof(SettingsPage));
        Routing.RegisterRoute("trade-fair-selection", typeof(TradeFairSelectionPage));
    }
}
