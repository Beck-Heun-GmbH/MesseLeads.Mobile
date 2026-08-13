using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MesseLeads.Mobile.Services;

namespace MesseLeads.Mobile.ViewModels;

public partial class ChangePasswordViewModel : BaseViewModel
{
    private readonly MobileAuthClient _authClient;
    private readonly AuthSessionService _sessionService;
    private readonly AppStartupService _startupService;

    [ObservableProperty]
    private string newPassword = string.Empty;

    [ObservableProperty]
    private string confirmPassword = string.Empty;

    [ObservableProperty]
    private string errorText = string.Empty;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    public ChangePasswordViewModel(
        MobileAuthClient authClient,
        AuthSessionService sessionService,
        AppStartupService startupService)
    {
        _authClient = authClient;
        _sessionService = sessionService;
        _startupService = startupService;

        Title = "Passwort festlegen";
    }

    partial void OnErrorTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorText = string.Empty;

        var password = NewPassword?.Trim() ?? string.Empty;
        var confirmation = ConfirmPassword?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(confirmation))
        {
            ErrorText = "Bitte beide Passwortfelder ausfüllen.";
            return;
        }

        if (password.Length < 8)
        {
            ErrorText = "Das Passwort muss mindestens 8 Zeichen lang sein.";
            return;
        }

        if (!string.Equals(
                password,
                confirmation,
                StringComparison.Ordinal))
        {
            ErrorText = "Die eingegebenen Passwörter stimmen nicht überein.";
            return;
        }

        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;

            var result = await _authClient.ChangePasswordAsync(password);

            if (!result.Success)
            {
                ErrorText =
                    result.Error ??
                    "Das Passwort konnte nicht geändert werden.";

                return;
            }

            await _sessionService.MarkPasswordChangedAsync();

            NewPassword = string.Empty;
            ConfirmPassword = string.Empty;

            await Shell.Current.DisplayAlert(
                "Passwort gespeichert",
                "Dein persönliches Passwort wurde erfolgreich gespeichert.",
                "OK");

            await _startupService.ContinueAfterAuthenticationAsync();
        }
        catch (Exception ex)
        {
            ErrorText =
                "Das Passwort konnte nicht geändert werden: " +
                ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
