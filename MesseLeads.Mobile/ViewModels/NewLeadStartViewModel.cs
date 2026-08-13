using CommunityToolkit.Mvvm.Input;
using MesseLeads.Mobile.Services;
using MesseLeads.Mobile.Models;

namespace MesseLeads.Mobile.ViewModels;

public partial class NewLeadStartViewModel : BaseViewModel
{
    private readonly INavigationService _navigationService;

    public NewLeadStartViewModel(
        INavigationService navigationService)
    {
        _navigationService = navigationService;
        Title = "Neuer Lead";
    }

    [RelayCommand]
    private Task StartWithPhotoAsync()
    {
        return _navigationService.GoToLeadWizardAsync(startMode: LeadStartMode.Photo);
    }

    [RelayCommand]
    private Task StartWithGalleryAsync()
    {
        return _navigationService.GoToLeadWizardAsync(startMode: LeadStartMode.Gallery);
    }

    [RelayCommand]
    private Task StartWithQrAsync()
    {
        return _navigationService.GoToLeadWizardAsync(startMode: LeadStartMode.Qr);
    }

    [RelayCommand]
    private Task StartManualAsync()
    {
        return _navigationService.GoToLeadWizardAsync(startMode: LeadStartMode.Manual);
    }

    [RelayCommand]
    private Task CancelAsync()
    {
        return _navigationService.GoBackAsync();
    }
}
