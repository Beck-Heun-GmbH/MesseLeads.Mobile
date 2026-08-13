using MesseLeads.Mobile.ViewModels;

namespace MesseLeads.Mobile.Views;

public partial class TradeFairSelectionPage : ContentPage
{
    private readonly TradeFairSelectionViewModel _viewModel;

    public TradeFairSelectionPage(TradeFairSelectionViewModel viewModel)
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
