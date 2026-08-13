using System.Diagnostics;
using MesseLeads.Mobile.Models;

namespace MesseLeads.Mobile.Services;

public sealed class LocalAiService : ILocalAiService
{
    private readonly QwenBusinessCardService _qwenService;
    private readonly HeuristicBusinessCardService _fallbackService;
    private readonly LocalLlmModelService _modelService;
    private readonly ILocalLlmRuntime _runtime;

    public LocalAiService(
        QwenBusinessCardService qwenService,
        HeuristicBusinessCardService fallbackService,
        LocalLlmModelService modelService,
        ILocalLlmRuntime runtime)
    {
        _qwenService = qwenService;
        _fallbackService = fallbackService;
        _modelService = modelService;
        _runtime = runtime;
    }

    public async Task<BusinessCardAiResult> ReadBusinessCardAsync(
        string? ocrText,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ocrText))
        {
            return new BusinessCardAiResult
            {
                RawText = ocrText ?? "",
                Engine = "Keine Eingabe",
                DiagnosticError = "Es wurde kein OCR-Text übergeben."
            };
        }

        var totalWatch = Stopwatch.StartNew();

        if (_qwenService.IsRuntimeAvailable)
        {
            try
            {
                return await _qwenService.ReadBusinessCardAsync(ocrText, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var fallback = _fallbackService.Read(ocrText);
                var modelState = await _modelService.GetStateAsync(cancellationToken);
                totalWatch.Stop();

                fallback.Engine = "Lokale Regeln (Qwen-Fehler)";
                fallback.NativeRuntimeAvailable = true;
                fallback.UsedFallback = true;
                fallback.ModelStatus = modelState.StatusText;
                fallback.ModelSizeBytes = modelState.SizeBytes;
                fallback.ModelPath = modelState.ModelPath;
                fallback.DiagnosticError = $"{ex.GetType().Name}: {ex.Message}";
                fallback.TotalDurationMs = totalWatch.ElapsedMilliseconds;
                return fallback;
            }
        }

        var result = _fallbackService.Read(ocrText);
        var state = await _modelService.GetStateAsync(cancellationToken);
        totalWatch.Stop();

        result.Engine = "Lokale Regeln (Runtime nicht verfügbar)";
        result.NativeRuntimeAvailable = false;
        result.UsedFallback = true;
        result.ModelStatus = state.StatusText;
        result.ModelSizeBytes = state.SizeBytes;
        result.ModelPath = state.ModelPath;
#if IOS
        result.DiagnosticError = _runtime.AvailabilityError ??
            "Die native llama.cpp-Runtime konnte nicht geladen oder initialisiert werden.";
#else
        result.DiagnosticError = _runtime.AvailabilityError ??
            "Die native llama.cpp-Runtime ist fuer diese Plattform nicht aktiviert.";
#endif
        result.TotalDurationMs = totalWatch.ElapsedMilliseconds;
        return result;
    }
}
