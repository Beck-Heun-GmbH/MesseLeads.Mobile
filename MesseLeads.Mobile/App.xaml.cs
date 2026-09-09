using MesseLeads.Mobile.Services;
using System.Diagnostics;

namespace MesseLeads.Mobile;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    private bool _startupDispatched;

    public App(IServiceProvider services)
    {
        InitializeComponent();

        Services = services;
    }

    /// <summary>
    /// Erzeugt das Hauptfenster mit der AppShell als Wurzelseite.
    /// </summary>
    /// <remarks>
    /// Ersetzt das frueher im Konstruktor gesetzte <c>MainPage</c>, das als
    /// veraltet markiert ist. Der Startlauf haengt jetzt an der Fenstererzeugung
    /// statt am Konstruktor, damit die Shell beim Ausfuehren sicher existiert –
    /// der Fehlerpfad navigiert ueber <c>Shell.Current</c>.
    /// </remarks>
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(Services.GetRequiredService<AppShell>());

        if (!_startupDispatched)
        {
            _startupDispatched = true;
            DispatchStartup();
        }

        return window;
    }

    private void DispatchStartup()
    {
        Dispatcher.Dispatch(async () =>
        {
            try
            {
                var startupService = Services.GetRequiredService<AppStartupService>();
                await startupService.StartAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("App-Startup fehlgeschlagen: " + ex);

                try
                {
                    var sessionService = Services.GetService<AuthSessionService>();
                    sessionService?.Logout();
                    await Shell.Current.GoToAsync("//login");
                }
                catch (Exception navigationException)
                {
                    Debug.WriteLine("Fallback-Navigation zum Login fehlgeschlagen: " + navigationException);
                }
            }
        });
    }

    protected override void OnResume()
    {
        base.OnResume();

        Dispatcher.Dispatch(async () =>
        {
            try
            {
                var startupService = Services.GetRequiredService<AppStartupService>();
                await startupService.RefreshLookupsOnResumeAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Stammdaten-Abgleich beim Fortsetzen fehlgeschlagen: " + ex);
            }
        });
    }
}
