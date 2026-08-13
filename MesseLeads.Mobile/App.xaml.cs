using MesseLeads.Mobile.Services;
using System.Diagnostics;

namespace MesseLeads.Mobile;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public App(IServiceProvider services)
    {
        InitializeComponent();

        Services = services;
        MainPage = services.GetRequiredService<AppShell>();

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
