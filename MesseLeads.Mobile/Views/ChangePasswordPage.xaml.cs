using MesseLeads.Mobile.ViewModels;

namespace MesseLeads.Mobile.Views;

public partial class ChangePasswordPage : ContentPage
{
    public ChangePasswordPage(
        ChangePasswordViewModel viewModel)
    {
        InitializeComponent();

        BindingContext = viewModel;
    }

    protected override bool OnBackButtonPressed()
    {
        // Solange das Initialpasswort nicht geändert wurde,
        // darf die Seite nicht übersprungen werden.
        return true;
    }
}