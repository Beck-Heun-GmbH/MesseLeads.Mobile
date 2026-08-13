using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MesseLeads.Mobile.Dtos;
using MesseLeads.Mobile.Models;

namespace MesseLeads.Mobile.Services;

public sealed class LeadSyncService
{
    private readonly HttpClient _client;
    private readonly LocalLeadService _localLeadService;
    private readonly LocalLeadImageService _imageService;
    private readonly SyncHistoryService _syncHistoryService;
    private readonly TradeFairSelectionService _tradeFairSelectionService;

    public LeadSyncService(
        HttpClient client,
        LocalLeadService localLeadService,
        LocalLeadImageService imageService,
        SyncHistoryService syncHistoryService,
        TradeFairSelectionService tradeFairSelectionService)
    {
        _client = client;
        _localLeadService = localLeadService;
        _imageService = imageService;
        _syncHistoryService = syncHistoryService;
        _tradeFairSelectionService = tradeFairSelectionService;
    }

    public Task<LeadSyncResult> SyncPendingAsync(CancellationToken cancellationToken = default)
    {
        return SyncPendingAsync("Manuell", cancellationToken);
    }

    public async Task<LeadSyncResult> SyncPendingAsync(
        string trigger,
        CancellationToken cancellationToken = default)
    {
        var startedUtc = DateTime.UtcNow;
        LeadSyncResult result;

        try
        {
            result = await SyncPendingCoreAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            result = new LeadSyncResult
            {
                Success = false,
                Failed = 1,
                Message = "Synchronisation fehlgeschlagen: " + ex.Message
            };
        }

        await _syncHistoryService.AddAsync(
            startedUtc,
            DateTime.UtcNow,
            result,
            trigger);

        return result;
    }

    private async Task<LeadSyncResult> SyncPendingCoreAsync(CancellationToken cancellationToken)
    {
        var token = await SecureStorage.GetAsync("auth_token");

        if (string.IsNullOrWhiteSpace(token))
        {
            return new LeadSyncResult
            {
                Success = false,
                Message = "Kein Login-Token vorhanden."
            };
        }

        var pending = await _localLeadService.GetPendingSyncAsync();
        var successCount = 0;
        var failedCount = 0;
        var imageSuccessCount = 0;
        var imageFailedCount = 0;
        var uploadedLeadIds = new HashSet<Guid>();
        var errorMessages = new List<string>();

        foreach (var lead in pending)
        {
            try
            {
                var leadResult = await UploadLeadAsync(lead, token, cancellationToken);

                if (!leadResult.Success || leadResult.ServerId is null)
                {
                    failedCount++;
                    await _localLeadService.MarkSyncFailedAsync(
                        lead,
                        leadResult.Error ?? "Lead-Upload fehlgeschlagen.");
                    errorMessages.Add($"Lead {ShortId(lead.LocalId)}: {leadResult.Error ?? "Upload fehlgeschlagen."}");
                    continue;
                }

                lead.TradeFairKey = leadResult.TradeFairKey ?? lead.TradeFairKey;
                lead.TradeFair = leadResult.TradeFair ?? lead.TradeFair;
                await _localLeadService.MarkSyncedAsync(lead, leadResult.ServerId.Value);
                successCount++;
                uploadedLeadIds.Add(lead.LocalId);

                var imageResult = await UploadImagesForLeadAsync(lead, token, cancellationToken);
                imageSuccessCount += imageResult.SuccessCount;
                imageFailedCount += imageResult.FailedCount;
                errorMessages.AddRange(imageResult.Errors);
            }
            catch (Exception ex)
            {
                failedCount++;
                await _localLeadService.MarkSyncFailedAsync(lead, ex.Message);
                errorMessages.Add($"Lead {ShortId(lead.LocalId)}: {ex.Message}");
            }
        }

        var syncedLeads = await _localLeadService.GetAllAsync();

        foreach (var lead in syncedLeads.Where(x => x.ServerId is not null && !uploadedLeadIds.Contains(x.LocalId)))
        {
            var imageResult = await UploadImagesForLeadAsync(lead, token, cancellationToken);
            imageSuccessCount += imageResult.SuccessCount;
            imageFailedCount += imageResult.FailedCount;
            errorMessages.AddRange(imageResult.Errors);
        }

        var totalFailures = failedCount + imageFailedCount;
        var message = BuildSyncMessage(successCount, imageSuccessCount, totalFailures, errorMessages);

        return new LeadSyncResult
        {
            Success = totalFailures == 0,
            Total = pending.Count,
            Synced = successCount,
            Failed = failedCount,
            ImageSynced = imageSuccessCount,
            ImageFailed = imageFailedCount,
            Message = message
        };
    }

    private async Task<MobileLeadUploadResponse> UploadLeadAsync(
        LocalLead lead,
        string token,
        CancellationToken cancellationToken)
    {
        var request = await BuildRequestAsync(lead);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/mobile/sync/lead")
        {
            Content = JsonContent.Create(request)
        };

        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _client.SendAsync(httpRequest, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new MobileLeadUploadResponse
            {
                Success = false,
                Error = $"HTTP {(int)response.StatusCode}: {raw}"
            };
        }

        return JsonSerializer.Deserialize<MobileLeadUploadResponse>(
            raw,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new MobileLeadUploadResponse
            {
                Success = false,
                Error = "Ungültige Serverantwort."
            };
    }

    private async Task<ImageSyncResult> UploadImagesForLeadAsync(
        LocalLead lead,
        string token,
        CancellationToken cancellationToken)
    {
        var images = await _imageService.GetPendingUploadForLeadAsync(lead.LocalId);

        var success = 0;
        var failed = 0;
        var errors = new List<string>();

        foreach (var image in images)
        {
            if (!File.Exists(image.LocalFilePath))
            {
                failed++;
                await _imageService.MarkMissingAsync(image);
                errors.Add($"Bild {ShortId(image.LocalImageId)}: lokale Datei nicht gefunden.");
                continue;
            }

            try
            {
                await _imageService.OptimizeExistingImageForUploadAsync(image);

                await using var fileStream = File.OpenRead(image.LocalFilePath);

                using var content = new MultipartFormDataContent();

                content.Add(new StringContent(lead.LocalId.ToString()), "mobileLocalId");
                content.Add(new StringContent(image.LocalImageId.ToString()), "mobileLocalImageId");
                content.Add(new StringContent(image.ImageType), "imageType");

                var fileContent = new StreamContent(fileStream);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");

                content.Add(fileContent, "file", Path.GetFileName(image.LocalFilePath));

                using var request = new HttpRequestMessage(HttpMethod.Post, "api/mobile/sync/lead-image")
                {
                    Content = content
                };

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                using var response = await _client.SendAsync(request, cancellationToken);
                var raw = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    failed++;
                    errors.Add($"Bild {ShortId(image.LocalImageId)}: {FormatHttpError((int)response.StatusCode, raw)}");
                    continue;
                }

                var result = JsonSerializer.Deserialize<MobileImageUploadResponse>(
                    raw,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result?.Success == true && result.ServerImageId is not null)
                {
                    await _imageService.MarkSyncedAsync(image, result.ServerImageId.Value);
                    success++;
                }
                else
                {
                    failed++;
                    errors.Add($"Bild {ShortId(image.LocalImageId)}: {result?.Error ?? "Ungültige Serverantwort."}");
                }
            }
            catch (Exception ex)
            {
                failed++;
                errors.Add($"Bild {ShortId(image.LocalImageId)}: {ex.Message}");
            }
        }

        return new ImageSyncResult
        {
            SuccessCount = success,
            FailedCount = failed,
            Errors = errors
        };
    }

    private async Task<MobileLeadUploadRequest> BuildRequestAsync(LocalLead lead)
    {
        var deviceId = await SecureStorage.GetAsync("device_id") ?? "";
        var selectedTradeFair = await _tradeFairSelectionService.GetSelectedAsync();
        var tradeFairKey = string.IsNullOrWhiteSpace(lead.TradeFairKey)
            ? selectedTradeFair?.Key
            : lead.TradeFairKey;
        var tradeFairLabel = string.IsNullOrWhiteSpace(lead.TradeFair)
            ? selectedTradeFair?.Label
            : lead.TradeFair;
        var selections = DeserializeSelections(lead.SelectionsJson);

        return new MobileLeadUploadRequest
        {
            MobileLocalId = lead.LocalId,
            DeviceId = deviceId,

            FirstName = lead.FirstName,
            LastName = lead.LastName,
            Company = lead.Company,
            JobTitle = lead.JobTitle,
            Email = lead.Email,
            Phone = lead.Phone,
            Mobile = lead.Mobile,
            Street = lead.Street,
            ZipCode = lead.ZipCode,
            City = lead.City,
            Country = lead.Country,
            Website = lead.Website,

            TradeFairKey = tradeFairKey,
            TradeFair = tradeFairLabel,
            VisitDay = lead.VisitDay,
            OwnerUser = lead.OwnerUser,
            WantsNewsletter = lead.WantsNewsletter,
            Notes = lead.Notes,
            FollowUpNotes = lead.FollowUpNotes,

            Selections = selections
        };
    }

    private static List<MobileLeadSelectionDto> DeserializeSelections(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var selections = JsonSerializer.Deserialize<List<LocalSelection>>(json) ?? [];

            return selections.Select(x => new MobileLeadSelectionDto
            {
                Group = x.Group,
                Key = x.Key,
                Value = x.Value,
                Print = x.Print,
                Digital = x.Digital
            }).ToList();
        }
        catch
        {
            return [];
        }
    }

    private static string BuildSyncMessage(
        int leadSuccessCount,
        int imageSuccessCount,
        int totalFailures,
        IReadOnlyCollection<string> errorMessages)
    {
        var summary = totalFailures == 0
            ? $"{leadSuccessCount} Lead(s) und {imageSuccessCount} Bild(er) synchronisiert."
            : $"{leadSuccessCount} Lead(s), {imageSuccessCount} Bild(er) synchronisiert. {totalFailures} Fehler.";

        if (errorMessages.Count == 0)
        {
            return summary;
        }

        return summary + " " + string.Join(" ", errorMessages.Take(3));
    }

    private static string ShortId(Guid id)
    {
        return id.ToString("N")[..8];
    }

    private static string TrimForMessage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Keine Serverdetails.";
        }

        value = value.ReplaceLineEndings(" ").Trim();

        return value.Length <= 180
            ? value
            : value[..180] + "...";
    }

    private static string FormatHttpError(int statusCode, string raw)
    {
        var message = ExtractProblemDetailsMessage(raw);

        return string.IsNullOrWhiteSpace(message)
            ? $"HTTP {statusCode}: {TrimForMessage(raw)}"
            : $"HTTP {statusCode}: {message}";
    }

    private static string ExtractProblemDetailsMessage(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "";
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;
            var parts = new List<string>();

            if (root.TryGetProperty("title", out var title) &&
                title.ValueKind == JsonValueKind.String)
            {
                parts.Add(title.GetString() ?? "");
            }

            if (root.TryGetProperty("errors", out var errors) &&
                errors.ValueKind == JsonValueKind.Object)
            {
                foreach (var errorProperty in errors.EnumerateObject())
                {
                    if (errorProperty.Value.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var error in errorProperty.Value.EnumerateArray())
                    {
                        if (error.ValueKind == JsonValueKind.String)
                        {
                            parts.Add(error.GetString() ?? "");
                        }
                    }
                }
            }

            var message = string.Join(" ", parts.Where(x => !string.IsNullOrWhiteSpace(x)));

            return TrimForMessage(message);
        }
        catch
        {
            return "";
        }
    }

    private sealed class ImageSyncResult
    {
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }
        public List<string> Errors { get; set; } = [];
    }
}

public sealed class LeadSyncResult
{
    public bool Success { get; set; }
    public int Total { get; set; }
    public int Synced { get; set; }
    public int Failed { get; set; }
    public int ImageSynced { get; set; }
    public int ImageFailed { get; set; }
    public string Message { get; set; } = "";
}
