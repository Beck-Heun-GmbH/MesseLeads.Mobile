using System.Diagnostics;
using MesseLeads.Mobile.ViewModels;

namespace MesseLeads.Mobile.Views;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _viewModel;

    public HomePage(HomeViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            await _viewModel.LoadAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine("HomePage konnte nicht geladen werden: " + ex);
            await Shell.Current.DisplayAlert(
                "Startproblem",
                "Die Startseite konnte nicht geladen werden. Bitte melde dich erneut an.",
                "OK");
            await Shell.Current.GoToAsync("//login");
        }
    }
}
