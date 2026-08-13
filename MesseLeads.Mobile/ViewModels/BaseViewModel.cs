using CommunityToolkit.Mvvm.ComponentModel;

namespace MesseLeads.Mobile.ViewModels;

public abstract partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string title = "";
}