namespace MesseLeads.Mobile.Services;

public sealed class UnavailableLocalLlmRuntime : ILocalLlmRuntime
{
    public bool IsAvailable => false;
    public string? AvailabilityError =>
        "Die native llama.cpp-Runtime ist fuer diese Plattform nicht aktiviert.";

    public Task<string> GenerateAsync(
        string modelPath,
        string prompt,
        int contextSize,
        int maxOutputTokens,
        int threadCount,
        float temperature,
        float topP,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            "Die native llama.cpp-Laufzeit ist noch nicht eingebunden. " +
            "Diese wird in der naechsten Einbaurunde ueber ein iOS-XCFramework angebunden.");
    }
}
