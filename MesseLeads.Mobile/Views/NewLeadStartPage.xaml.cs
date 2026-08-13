using MesseLeads.Mobile.ViewModels;

namespace MesseLeads.Mobile.Views;

public partial class NewLeadStartPage : ContentPage
{
    public NewLeadStartPage(NewLeadStartViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}