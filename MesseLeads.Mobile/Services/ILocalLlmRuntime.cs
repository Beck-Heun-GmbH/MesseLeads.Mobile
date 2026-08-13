namespace MesseLeads.Mobile.Services;

public interface ILocalLlmRuntime
{
    bool IsAvailable { get; }
    string? AvailabilityError { get; }

    Task<string> GenerateAsync(
        string modelPath,
        string prompt,
        int contextSize,
        int maxOutputTokens,
        int threadCount,
        float temperature,
        float topP,
        CancellationToken cancellationToken = default);
}
