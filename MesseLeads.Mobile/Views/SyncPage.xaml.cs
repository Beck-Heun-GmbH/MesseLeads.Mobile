using MesseLeads.Mobile.ViewModels;

namespace MesseLeads.Mobile.Views;

public partial class SyncPage : ContentPage
{
    private readonly SyncViewModel _viewModel;

    public SyncPage(SyncViewModel viewModel)
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