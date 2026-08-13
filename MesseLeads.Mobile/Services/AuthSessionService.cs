using MesseLeads.Mobile.Models;
using System.Diagnostics;

namespace MesseLeads.Mobile.Services;

public sealed class AuthSessionService
{
    private const string TokenKey = "auth_token";
    private const string DisplayNameKey = "display_name";
    private const string UserIdKey = "user_id";
    private const string LoginUtcKey = "login_utc";
    private const string ValidUntilUtcKey = "login_valid_until_utc";
    private const string MustChangePasswordKey = "must_change_password";
    private const string IsAdminKey = "is_admin";

    private static readonly TimeSpan SessionDuration = TimeSpan.FromDays(30);

    public async Task SaveLoginAsync(MobileLoginResponse response, string fallbackDisplayName)
    {
        if (string.IsNullOrWhiteSpace(response.Token))
        {
            throw new InvalidOperationException("LoginResponse enthält kein Token.");
        }

        var now = DateTime.UtcNow;
        var validUntil = response.ExpiresUtc?.ToUniversalTime() ?? now.Add(SessionDuration);

        await SecureStorage.SetAsync(TokenKey, response.Token);
        await SecureStorage.SetAsync(DisplayNameKey, response.DisplayName ?? fallbackDisplayName);
        await SecureStorage.SetAsync(UserIdKey, response.UserId?.ToString() ?? "");
        await SecureStorage.SetAsync(LoginUtcKey, now.ToString("O"));
        await SecureStorage.SetAsync(ValidUntilUtcKey, validUntil.ToString("O"));
        await SecureStorage.SetAsync(MustChangePasswordKey, response.MustChangePassword ? "true" : "false");
        await SecureStorage.SetAsync(
            IsAdminKey,
            IsAdminLogin(response, fallbackDisplayName) ? "true" : "false");
    }

    public async Task<bool> HasValidSessionAsync()
    {
        try
        {
            var token = await GetTokenAsync();

            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            var validUntilRaw = await ReadSecureStorageAsync(ValidUntilUtcKey);

            if (!DateTime.TryParse(validUntilRaw, out var validUntilUtc))
            {
                return false;
            }

            return validUntilUtc.ToUniversalTime() > DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Session konnte nicht gelesen werden: " + ex);
            return false;
        }
    }

    public async Task<bool> MustChangePasswordAsync()
    {
        var raw = await ReadSecureStorageAsync(MustChangePasswordKey);
        return string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
    }

    public async Task MarkPasswordChangedAsync()
    {
        await SecureStorage.SetAsync(MustChangePasswordKey, "false");

        var validUntil = DateTime.UtcNow.Add(SessionDuration);
        await SecureStorage.SetAsync(ValidUntilUtcKey, validUntil.ToString("O"));
    }

    public Task<string?> GetTokenAsync()
    {
        return ReadSecureStorageAsync(TokenKey);
    }

    public async Task<string> GetDisplayNameAsync()
    {
        return await ReadSecureStorageAsync(DisplayNameKey) ?? "Benutzer";
    }

    public async Task<int?> GetUserIdAsync()
    {
        var raw = await ReadSecureStorageAsync(UserIdKey);

        return int.TryParse(raw, out var userId)
            ? userId
            : null;
    }

    public async Task<bool> IsAdminAsync()
    {
        var raw = await ReadSecureStorageAsync(IsAdminKey);
        return string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
    }

    public Task<string?> GetValueAsync(string key)
    {
        return ReadSecureStorageAsync(key);
    }

    public async Task ExtendSessionAsync()
    {
        if (!await HasValidSessionAsync())
        {
            return;
        }

        var validUntil = DateTime.UtcNow.Add(SessionDuration);
        await SecureStorage.SetAsync(ValidUntilUtcKey, validUntil.ToString("O"));
    }

    public void Logout()
    {
        RemoveSecureStorage(TokenKey);
        RemoveSecureStorage(DisplayNameKey);
        RemoveSecureStorage(UserIdKey);
        RemoveSecureStorage(LoginUtcKey);
        RemoveSecureStorage(ValidUntilUtcKey);
        RemoveSecureStorage(MustChangePasswordKey);
        RemoveSecureStorage(IsAdminKey);
    }

    private static bool IsAdminLogin(
        MobileLoginResponse response,
        string fallbackDisplayName)
    {
        return response.IsAdmin ||
               IsAdminRole(fallbackDisplayName);
    }

    private static bool IsAdminRole(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        return string.Equals(normalized, "admin", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "administrator", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string?> ReadSecureStorageAsync(string key)
    {
        try
        {
            return await SecureStorage.GetAsync(key);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SecureStorage konnte '{key}' nicht lesen: {ex}");
            return null;
        }
    }

    private static void RemoveSecureStorage(string key)
    {
        try
        {
            SecureStorage.Remove(key);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SecureStorage konnte '{key}' nicht entfernen: {ex}");
        }
    }
}
