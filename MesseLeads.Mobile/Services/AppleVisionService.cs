#if IOS || MACCATALYST
using Foundation;
using Vision;

namespace MesseLeads.Mobile.Services;

/// <summary>
/// Liest Text aus Visitenkarten vollständig lokal mit Apple Vision aus.
/// Es werden keine Bild- oder Kontaktdaten an einen externen Dienst übertragen.
/// </summary>
public sealed class AppleVisionService : ILocalVisionService
{
    public async Task<string> ReadBusinessCardAsync(
        string imagePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            throw new ArgumentException("Es wurde kein Bildpfad übergeben.", nameof(imagePath));
        }

        if (!File.Exists(imagePath))
        {
            throw new FileNotFoundException("Das Visitenkartenfoto wurde nicht gefunden.", imagePath);
        }

        cancellationToken.ThrowIfCancellationRequested();

        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var imageUrl = NSUrl.FromFilename(imagePath);
            if (imageUrl is null)
            {
                throw new InvalidOperationException("Das Visitenkartenfoto konnte nicht geöffnet werden.");
            }

            string recognizedText = string.Empty;
            Exception? recognitionException = null;

            using var request = new VNRecognizeTextRequest((visionRequest, error) =>
            {
                if (error is not null)
                {
                    recognitionException = new InvalidOperationException(
                        $"Apple Vision konnte den Text nicht lesen: {error.LocalizedDescription}");
                    return;
                }

                var observations = visionRequest
                    .GetResults<VNRecognizedTextObservation>()
                    ?.OrderByDescending(x => x.BoundingBox.Y)
                    .ThenBy(x => x.BoundingBox.X)
                    .ToList() ?? [];

                var lines = new List<string>();

                foreach (var observation in observations)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var candidate = observation.TopCandidates(1).FirstOrDefault();
                    var line = candidate?.String?.Trim();

                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        lines.Add(line);
                    }
                }

                recognizedText = string.Join(Environment.NewLine, lines);
            })
            {
                RecognitionLevel = VNRequestTextRecognitionLevel.Accurate,
                UsesLanguageCorrection = true,
                RecognitionLanguages = ["de-DE", "en-US"],
                MinimumTextHeight = 0.008f
            };

            using var handler = new VNImageRequestHandler(imageUrl, new VNImageOptions());

            var success = handler.Perform([request], out var performError);

            if (!success || performError is not null)
            {
                throw new InvalidOperationException(
                    "Apple Vision konnte das Visitenkartenfoto nicht analysieren: " +
                    (performError?.LocalizedDescription ?? "Unbekannter Fehler."));
            }

            if (recognitionException is not null)
            {
                throw recognitionException;
            }

            return NormalizeRecognizedText(recognizedText);
        }, cancellationToken);
    }

    private static string NormalizeRecognizedText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var lines = text
            .Replace("\u00A0", " ", StringComparison.Ordinal)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => string.Join(
                " ",
                line.Split(' ', StringSplitOptions.RemoveEmptyEntries)))
            .Where(line => !string.IsNullOrWhiteSpace(line));

        return string.Join(Environment.NewLine, lines);
    }
}
#endif
