using System.Net.Http.Headers;
using System.Text.Json;
using MesseLeads.Mobile.Data;
using MesseLeads.Mobile.Dtos;
using MesseLeads.Mobile.Models;

namespace MesseLeads.Mobile.Services;

public sealed class LookupSyncService
{
    private const string TradeFairGroup = "TradeFair";
    private const string GroupsSyncedKey = "lookup_groups_synced";

    private static readonly JsonSerializerOptions SerializerOptions =
        new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _client;
    private readonly LocalDatabaseService _database;
    private readonly SemaphoreSlim _downloadLock = new(1, 1);

    public LookupSyncService(HttpClient client, LocalDatabaseService database)
    {
        _client = client;
        _database = database;
    }

    public async Task<int> DownloadAsync(CancellationToken cancellationToken = default)
    {
        await _downloadLock.WaitAsync(cancellationToken);

        try
        {
            return await DownloadCoreAsync(cancellationToken);
        }
        finally
        {
            _downloadLock.Release();
        }
    }

    private async Task<int> DownloadCoreAsync(CancellationToken cancellationToken)
    {
        var itemsRaw = await GetAsync("api/mobile/config/lookups", cancellationToken);

        var items = JsonSerializer.Deserialize<List<MobileLookupItemDto>>(
            itemsRaw,
            SerializerOptions);

        if (items is null)
        {
            return 0;
        }

        var groups = await DownloadGroupsAsync(cancellationToken);

        var db = await _database.GetDatabaseAsync();

        await db.RunInTransactionAsync(connection =>
        {
            connection.DeleteAll<LocalLookupItem>();

            foreach (var item in items)
            {
                connection.Insert(new LocalLookupItem
                {
                    TradeFairKey = item.TradeFairKey,
                    Group = item.Group,
                    Key = item.Key,
                    Label = item.Label,
                    SortOrder = item.SortOrder,
                    Description = item.Description,
                    FieldType = item.FieldType,
                    OptionA = item.OptionA,
                    OptionB = item.OptionB,
                    UpdatedUtc = DateTime.UtcNow
                });
            }

            if (groups is null)
            {
                return;
            }

            connection.DeleteAll<LocalLookupGroup>();

            foreach (var group in groups)
            {
                connection.Insert(new LocalLookupGroup
                {
                    TradeFairKey = group.TradeFairKey,
                    Key = group.Key,
                    Label = group.Label,
                    SortOrder = group.SortOrder,
                    Description = group.Description,
                    UpdatedUtc = DateTime.UtcNow
                });
            }
        });

        if (groups is not null)
        {
            // Ab jetzt sind die Bereiche des Servers maßgeblich - auch wenn er keine mehr liefert.
            Preferences.Set(GroupsSyncedKey, true);
        }

        return items.Count;
    }

    // Ein Server ohne diesen Endpunkt darf die Stammdaten-Synchronisation nicht scheitern lassen;
    // in dem Fall bleiben die zuletzt bekannten Bereiche erhalten.
    private async Task<List<MobileLookupGroupDto>?> DownloadGroupsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var raw = await GetAsync("api/mobile/config/lookup-groups", cancellationToken);

            return JsonSerializer.Deserialize<List<MobileLookupGroupDto>>(raw, SerializerOptions);
        }
        catch
        {
            return null;
        }
    }

    private async Task<string> GetAsync(string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);

        var token = await SecureStorage.GetAsync("auth_token");

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var httpResponse = await _client.SendAsync(request, cancellationToken);
        var raw = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"HTTP {(int)httpResponse.StatusCode}: {raw}");
        }

        return raw;
    }

    public async Task<List<LocalLookupItem>> GetAllAsync()
    {
        var db = await _database.GetDatabaseAsync();

        return await db.Table<LocalLookupItem>()
            .OrderBy(x => x.Group)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Label)
            .ToListAsync();
    }

    public async Task<List<LocalLookupItem>> GetByGroupAsync(string group, string? tradeFairKey = null)
    {
        var db = await _database.GetDatabaseAsync();

        return await db.Table<LocalLookupItem>()
            .Where(x => x.Group == group && x.TradeFairKey == tradeFairKey)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Label)
            .ToListAsync();
    }

    public async Task<List<LocalLookupGroup>> GetGroupsAsync(string? tradeFairKey = null)
    {
        var db = await _database.GetDatabaseAsync();

        var groups = await db.Table<LocalLookupGroup>()
            .Where(x => x.TradeFairKey == tradeFairKey)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Label)
            .ToListAsync();

        if (groups.Count > 0 || Preferences.Get(GroupsSyncedKey, false))
        {
            return groups;
        }

        // Gerät wurde aktualisiert, bevor der Server den Bereichs-Endpunkt kannte:
        // Bereiche aus den bereits vorhandenen Einträgen der Messe ableiten, damit offline nichts verloren geht.
        var items = await GetAllAsync();

        return items
            .Where(x => x.Group != TradeFairGroup && x.TradeFairKey == tradeFairKey)
            .GroupBy(x => x.Group)
            .Select(group => new LocalLookupGroup { TradeFairKey = tradeFairKey, Key = group.Key, Label = group.Key })
            .ToList();
    }

    public async Task<int> CountAsync()
    {
        var db = await _database.GetDatabaseAsync();

        return await db.Table<LocalLookupItem>().CountAsync();
    }
}
