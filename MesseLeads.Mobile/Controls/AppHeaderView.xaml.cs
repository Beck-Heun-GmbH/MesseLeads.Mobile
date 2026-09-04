namespace MesseLeads.Mobile.Controls;

public partial class AppHeaderView : ContentView
{
    public static readonly BindableProperty GreetingProperty =
        BindableProperty.Create(nameof(Greeting), typeof(string), typeof(AppHeaderView), "FoxyLeads");

    public static readonly BindableProperty SubtitleProperty =
        BindableProperty.Create(nameof(Subtitle), typeof(string), typeof(AppHeaderView), "FoxyLeads");

    public static readonly BindableProperty ShowBackButtonProperty =
        BindableProperty.Create(nameof(ShowBackButton), typeof(bool), typeof(AppHeaderView), false);

    public static readonly BindableProperty ShowMenuButtonProperty =
        BindableProperty.Create(nameof(ShowMenuButton), typeof(bool), typeof(AppHeaderView), true);

    public static readonly BindableProperty HomeEnabledProperty =
        BindableProperty.Create(nameof(HomeEnabled), typeof(bool), typeof(AppHeaderView), true);

    public static readonly BindableProperty MenuCommandProperty =
        BindableProperty.Create(nameof(MenuCommand), typeof(Command), typeof(AppHeaderView));

    public string Greeting
    {
        get => (string)GetValue(GreetingProperty);
        set => SetValue(GreetingProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public bool ShowBackButton
    {
        get => (bool)GetValue(ShowBackButtonProperty);
        set => SetValue(ShowBackButtonProperty, value);
    }

    public bool ShowMenuButton
    {
        get => (bool)GetValue(ShowMenuButtonProperty);
        set => SetValue(ShowMenuButtonProperty, value);
    }

    public bool HomeEnabled
    {
        get => (bool)GetValue(HomeEnabledProperty);
        set => SetValue(HomeEnabledProperty, value);
    }

    public Command? MenuCommand
    {
        get => (Command?)GetValue(MenuCommandProperty);
        set => SetValue(MenuCommandProperty, value);
    }

    public AppHeaderView()
    {
        InitializeComponent();
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnHomeTapped(object sender, TappedEventArgs e)
    {
        if (!HomeEnabled)
        {
            return;
        }

        await Shell.Current.GoToAsync("//home");
    }

    private async void OnMenuTapped(object sender, TappedEventArgs e)
    {
        var action = await Shell.Current.DisplayActionSheet(
            "FoxyLeads",
            "Abbrechen",
            null,
            "Entwürfe öffnen",
            "Verlauf öffnen",
            "Synchronisieren",
            "Einstellungen",
            "Startseite");

        switch (action)
        {
            case "Entwürfe öffnen":
                await Shell.Current.GoToAsync("drafts");
                break;

            case "Verlauf öffnen":
                await Shell.Current.GoToAsync("history");
                break;

            case "Synchronisieren":
                await Shell.Current.GoToAsync("sync");
                break;

            case "Einstellungen":
                await Shell.Current.GoToAsync("settings");
                break;

            case "Startseite":
                await Shell.Current.GoToAsync("//home");
                break;
        }
    }
}
