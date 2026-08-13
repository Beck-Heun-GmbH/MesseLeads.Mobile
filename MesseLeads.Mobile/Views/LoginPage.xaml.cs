using MesseLeads.Mobile.Services;

namespace MesseLeads.Mobile.Views;

public partial class LoginPage : ContentPage
{
    private readonly MobileAuthClient _authClient;
    private readonly AuthSessionService _sessionService;
    private readonly INavigationService _navigationService;
    private readonly AppStartupService _startupService;

    public LoginPage(
        MobileAuthClient authClient,
        AuthSessionService sessionService,
        INavigationService navigationService,
        AppStartupService startupService)
    {
        InitializeComponent();
        _authClient = authClient;
        _sessionService = sessionService;
        _navigationService = navigationService;
        _startupService = startupService;
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;

        var username = UsernameEntry.Text?.Trim() ?? "";
        var password = PasswordEntry.Text ?? "";

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ShowError("Bitte Benutzername und Passwort eingeben.");
            return;
        }

        try
        {
            SetLoading(true);

            var result = await _authClient.LoginAsync(username, password);

            if (!result.Success || string.IsNullOrWhiteSpace(result.Token))
            {
                ShowError(result.Error ?? "Login fehlgeschlagen.");
                return;
            }

            await _sessionService.SaveLoginAsync(result, username);

            if (result.MustChangePassword)
            {
                await _navigationService.GoToChangePasswordAsync();
                return;
            }

            await _startupService.ContinueAfterAuthenticationAsync();
        }
        catch (Exception ex)
        {
            ShowError("Login konnte nicht ausgeführt werden: " + ex.Message);
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void SetLoading(bool isLoading)
    {
        LoginButton.IsEnabled = !isLoading;
        LoadingIndicator.IsVisible = isLoading;
        LoadingIndicator.IsRunning = isLoading;
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }
}
