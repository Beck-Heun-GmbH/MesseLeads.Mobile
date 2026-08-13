using MesseLeads.Mobile.ViewModels;

namespace MesseLeads.Mobile.Views;

public partial class DraftsPage : ContentPage
{
    private readonly DraftsViewModel _viewModel;

    public DraftsPage(DraftsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}