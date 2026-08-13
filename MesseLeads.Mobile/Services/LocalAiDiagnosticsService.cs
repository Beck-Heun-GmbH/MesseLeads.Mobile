using MesseLeads.Mobile.Models;

namespace MesseLeads.Mobile.Services;

public sealed class LocalAiDiagnosticsService
{
    private readonly ILocalLlmRuntime _runtime;
    private readonly LocalLlmModelService _modelService;

    public LocalAiDiagnosticsService(
        ILocalLlmRuntime runtime,
        LocalLlmModelService modelService)
    {
        _runtime = runtime;
        _modelService = modelService;
    }

    public async Task<LocalAiDiagnosticsSnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        var modelState = await _modelService.GetStateAsync(cancellationToken);
        var packagedModelFound = await IsPackagedModelAvailableAsync(cancellationToken);
        var runtimeAvailable = _runtime.IsAvailable;

        var runtimeStatus = runtimeAvailable
            ? "llama.cpp Runtime verfügbar"
            : string.IsNullOrWhiteSpace(_runtime.AvailabilityError)
                ? "llama.cpp Runtime nicht verfügbar – Fallback auf lokale Regeln"
                : $"llama.cpp Runtime nicht verfügbar: {_runtime.AvailabilityError}";

        var modelStatus = modelState.IsInstalled
            ? modelState.StatusText
            : packagedModelFound
                ? "Qwen-Modell ist im App-Paket vorhanden und wird beim ersten Lauf lokal kopiert."
                : $"Qwen-Modell ist weder lokal installiert noch im App-Paket unter '{LocalLlmOptions.PackagedModelPath}' gefunden.";

        return new LocalAiDiagnosticsSnapshot
        {
            RuntimeAvailable = runtimeAvailable,
            ModelInstalled = modelState.IsInstalled,
            PackagedModelFound = packagedModelFound,
            RuntimeStatus = runtimeStatus,
            ModelStatus = modelStatus,
            ModelPath = modelState.ModelPath,
            ModelSizeBytes = modelState.SizeBytes
        };
    }

    private static async Task<bool> IsPackagedModelAvailableAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await FileSystem.OpenAppPackageFileAsync(
                LocalLlmOptions.PackagedModelPath);

            cancellationToken.ThrowIfCancellationRequested();
            return stream.CanRead;
        }
        catch
        {
            return false;
        }
    }
}
