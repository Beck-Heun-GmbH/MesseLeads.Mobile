using System.Diagnostics;
using MesseLeads.Mobile.Models;

namespace MesseLeads.Mobile.Services;

public sealed class QwenBusinessCardService
{
    private readonly ILocalLlmRuntime _runtime;
    private readonly LocalLlmModelService _modelService;
    private readonly QwenBusinessCardPromptBuilder _promptBuilder;
    private readonly QwenBusinessCardJsonParser _jsonParser;
    private readonly LocalLlmOptions _options;

    public QwenBusinessCardService(
        ILocalLlmRuntime runtime,
        LocalLlmModelService modelService,
        QwenBusinessCardPromptBuilder promptBuilder,
        QwenBusinessCardJsonParser jsonParser,
        LocalLlmOptions options)
    {
        _runtime = runtime;
        _modelService = modelService;
        _promptBuilder = promptBuilder;
        _jsonParser = jsonParser;
        _options = options;
    }

    public bool IsRuntimeAvailable => _runtime.IsAvailable;

    public async Task<BusinessCardAiResult> ReadBusinessCardAsync(
        string ocrText,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ocrText);

        if (!_runtime.IsAvailable)
        {
            throw new NotSupportedException(
                _runtime.AvailabilityError ??
                "Die llama.cpp-Laufzeit ist auf diesem Build nicht verfuegbar.");
        }

        var totalWatch = Stopwatch.StartNew();
        var modelWatch = Stopwatch.StartNew();
        var model = await _modelService.EnsureInstalledFromAppPackageAsync(cancellationToken);
        modelWatch.Stop();

        if (!model.IsInstalled)
        {
            throw new InvalidOperationException(model.StatusText);
        }

        var prompt = _promptBuilder.Build(ocrText);

        var inferenceWatch = Stopwatch.StartNew();
        var generated = await _runtime.GenerateAsync(
            model.ModelPath,
            prompt,
            _options.ContextSize,
            _options.MaxOutputTokens,
            _options.CpuThreadCount,
            _options.Temperature,
            _options.TopP,
            cancellationToken);
        inferenceWatch.Stop();

        var jsonWatch = Stopwatch.StartNew();
        var json = _jsonParser.ExtractJsonObject(generated);
        var result = _jsonParser.ParseJson(json, ocrText);
        jsonWatch.Stop();
        totalWatch.Stop();

        result.NativeRuntimeAvailable = true;
        result.UsedFallback = false;
        result.ModelStatus = model.StatusText;
        result.ModelSizeBytes = model.SizeBytes;
        result.ModelPath = model.ModelPath;
        result.PromptLength = prompt.Length;
        result.GeneratedText = generated;
        result.ModelPreparationDurationMs = modelWatch.ElapsedMilliseconds;
        result.InferenceDurationMs = inferenceWatch.ElapsedMilliseconds;
        result.JsonParsingDurationMs = jsonWatch.ElapsedMilliseconds;
        result.TotalDurationMs = totalWatch.ElapsedMilliseconds;

        return result;
    }
}
