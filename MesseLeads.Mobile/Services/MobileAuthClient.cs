using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MesseLeads.Mobile.Models;

namespace MesseLeads.Mobile.Services;

public sealed class MobileAuthClient
{
    private readonly HttpClient _httpClient;
    private readonly AuthSessionService _sessionService;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public MobileAuthClient(
        HttpClient httpClient,
        AuthSessionService sessionService)
    {
        _httpClient = httpClient;
        _sessionService = sessionService;
    }

    public async Task<MobileLoginResponse> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            username,
            password,
            deviceId = await GetDeviceIdAsync(),
            deviceName = DeviceInfo.Name,
            appVersion = AppInfo.VersionString
        };

        using var response = await _httpClient.PostAsJsonAsync(
            "api/mobile/auth/login",
            request,
            cancellationToken);

        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new MobileLoginResponse
            {
                Success = false,
                Error = ReadServerError(
                    raw,
                    $"Login fehlgeschlagen. HTTP {(int)response.StatusCode}.")
            };
        }

        try
        {
            return JsonSerializer.Deserialize<MobileLoginResponse>(
                       raw,
                       JsonOptions)
                   ?? new MobileLoginResponse
                   {
                       Success = false,
                       Error = "Die Serverantwort konnte nicht gelesen werden."
                   };
        }
        catch (JsonException)
        {
            return new MobileLoginResponse
            {
                Success = false,
                Error = "Die Serverantwort hatte ein ungültiges Format."
            };
        }
    }

    public async Task<MobileChangePasswordResponse> ChangePasswordAsync(
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var token = await _sessionService.GetTokenAsync();

        if (string.IsNullOrWhiteSpace(token))
        {
            return new MobileChangePasswordResponse
            {
                Success = false,
                Error = "Die Anmeldung ist nicht mehr gültig. Bitte erneut anmelden."
            };
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "api/mobile/auth/change-password");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        request.Content = JsonContent.Create(new
        {
            newPassword
        });

        using var response = await _httpClient.SendAsync(
            request,
            cancellationToken);

        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new MobileChangePasswordResponse
            {
                Success = false,
                Error = ReadServerError(
                    raw,
                    $"Das Passwort konnte nicht geändert werden. HTTP {(int)response.StatusCode}.")
            };
        }

        try
        {
            return JsonSerializer.Deserialize<MobileChangePasswordResponse>(
                       raw,
                       JsonOptions)
                   ?? new MobileChangePasswordResponse
                   {
                       Success = false,
                       Error = "Die Serverantwort konnte nicht gelesen werden."
                   };
        }
        catch (JsonException)
        {
            return new MobileChangePasswordResponse
            {
                Success = false,
                Error = "Die Serverantwort hatte ein ungültiges Format."
            };
        }
    }

    private static async Task<string> GetDeviceIdAsync()
    {
        const string key = "device_id";

        var existing = await SecureStorage.GetAsync(key);

        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        var deviceId = Guid.NewGuid().ToString("N");

        await SecureStorage.SetAsync(key, deviceId);

        return deviceId;
    }

    private static string ReadServerError(
        string raw,
        string fallback)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        try
        {
            using var document = JsonDocument.Parse(raw);

            if (document.RootElement.TryGetProperty(
                    "error",
                    out var errorElement))
            {
                var error = errorElement.GetString();

                if (!string.IsNullOrWhiteSpace(error))
                {
                    return error;
                }
            }
        }
        catch (JsonException)
        {
            // Bei nicht lesbarem JSON verwenden wir den Fallback.
        }

        return fallback;
    }
}