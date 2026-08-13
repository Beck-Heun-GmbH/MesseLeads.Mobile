using MesseLeads.Mobile.Data;
using MesseLeads.Mobile.Models;
using MesseLeads.Mobile.Services;
using MesseLeads.Mobile.ViewModels;
using MesseLeads.Mobile.Views;
using Microsoft.Extensions.Logging;
using ZXing.Net.Maui.Controls;

namespace MesseLeads.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseBarcodeReader()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        builder.Services.AddSingleton<HttpClient>(_ =>
        {
#if DEBUG
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            return new HttpClient(handler)
            {
                BaseAddress = new Uri("https://10.0.2.2:7013/")
            };
#else
            return new HttpClient
            {
                BaseAddress = new Uri("https://ml.beck-heun.de:8888/")
            };
#endif
        });

        builder.Services.AddSingleton<INavigationService, AppNavigationService>();
        builder.Services.AddSingleton<MobileAuthClient>();

        builder.Services.AddTransient<HomeViewModel>();
        builder.Services.AddTransient<TradeFairSelectionViewModel>();
        builder.Services.AddTransient<NewLeadStartViewModel>();
        builder.Services.AddTransient<LeadWizardViewModel>();
        builder.Services.AddTransient<DraftsViewModel>();
        builder.Services.AddTransient<HistoryViewModel>();
        builder.Services.AddTransient<SyncViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        builder.Services.AddSingleton<LocalDatabaseService>();
        builder.Services.AddSingleton<LocalLeadService>();
        builder.Services.AddSingleton<LookupSyncService>();
        builder.Services.AddSingleton<TradeFairSelectionService>();
        builder.Services.AddSingleton<SyncHistoryService>();
        builder.Services.AddSingleton<LeadSyncService>();
        builder.Services.AddSingleton<LocalLeadImageService>();
        builder.Services.AddSingleton<ILocalAiService, LocalAiService>();
        builder.Services.AddSingleton<VCardParserService>();
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddSingleton(new LocalLlmOptions());
        builder.Services.AddSingleton<LocalLlmModelService>();
        builder.Services.AddSingleton<QwenBusinessCardPromptBuilder>();
        builder.Services.AddSingleton<QwenBusinessCardJsonParser>();
        builder.Services.AddSingleton<QwenBusinessCardService>();
        builder.Services.AddSingleton<LocalAiDiagnosticsService>();
#if IOS
        builder.Services.AddSingleton<ILocalLlmRuntime, IosLlamaCppRuntime>();
#else
        builder.Services.AddSingleton<ILocalLlmRuntime, UnavailableLocalLlmRuntime>();
#endif

        builder.Services.AddSingleton<HeuristicBusinessCardService>();
#if IOS
        builder.Services.AddSingleton<ILocalVisionService, AppleVisionService>();
#else
        builder.Services.AddSingleton<ILocalVisionService, LocalVisionService>();
#endif
        builder.Services.AddSingleton<AuthSessionService>();
        builder.Services.AddSingleton<AppStartupService>();

        builder.Services.AddTransient<ChangePasswordViewModel>();
        builder.Services.AddTransient<ChangePasswordPage>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<TradeFairSelectionPage>();
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<NewLeadStartPage>();
        builder.Services.AddTransient<LeadWizardPage>();
        builder.Services.AddTransient<DraftsPage>();
        builder.Services.AddTransient<HistoryPage>();
        builder.Services.AddTransient<SyncPage>();
        builder.Services.AddTransient<SettingsPage>();

        return builder.Build();
    }
}
