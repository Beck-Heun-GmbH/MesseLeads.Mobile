namespace MesseLeads.Mobile.Services;

/// <summary>
/// Platzhalter für Plattformen, für die noch keine native OCR-Implementierung
/// eingebaut wurde. iOS und Mac Catalyst verwenden AppleVisionService.
/// </summary>
public sealed class LocalVisionService : ILocalVisionService
{
    public Task<string> ReadBusinessCardAsync(
        string imagePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        throw new PlatformNotSupportedException(
            "Die lokale Visitenkarten-OCR ist aktuell nur auf iOS verfügbar. " +
            "Die Android-Implementierung folgt in einer späteren Ausbaurunde.");
    }
}
