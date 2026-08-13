using System.Net.Http.Headers;
using System.Text.Json;
using MesseLeads.Mobile.Data;
using MesseLeads.Mobile.Dtos;
using MesseLeads.Mobile.Models;

namespace MesseLeads.Mobile.Services;

public sealed class TradeFairSelectionService
{
    private const string SelectedTradeFairKey = "selected_trade_fair_key";
    private const string SelectedTradeFairLabel = "selected_trade_fair_label";
    private const string TradeFairGroup = "TradeFair";

    private readonly HttpClient _client;
    private readonly LocalDatabaseService _database;

    public TradeFairSelectionService(HttpClient client, LocalDatabaseService database)
    {
        _client = client;
        _database = database;
    }

    public async Task<bool> HasSelectionAsync()
    {
        var key = await SecureStorage.GetAsync(SelectedTradeFairKey);
        var label = await SecureStorage.GetAsync(SelectedTradeFairLabel);

        return !string.IsNullOrWhiteSpace(key) &&
               !string.IsNullOrWhiteSpace(label);
    }

    public async Task<TradeFairSelection?> GetSelectedAsync()
    {
        var key = await SecureStorage.GetAsync(SelectedTradeFairKey);
        var label = await SecureStorage.GetAsync(SelectedTradeFairLabel);

        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(label))
        {
            return null;
        }

        return new TradeFairSelection
        {
            Key = key,
            Label = label
        };
    }

    public async Task SaveSelectionAsync(TradeFairSelection selection)
    {
        if (string.IsNullOrWhiteSpace(selection.Key) ||
            string.IsNullOrWhiteSpace(selection.Label))
        {
            throw new InvalidOperationException("Bitte eine gültige Messe auswählen.");
        }

        await SecureStorage.SetAsync(SelectedTradeFairKey, selection.Key);
        await SecureStorage.SetAsync(SelectedTradeFairLabel, selection.Label);
    }

    public async Task<List<TradeFairSelection>> GetLocalTradeFairsAsync()
    {
        var db = await _database.GetDatabaseAsync();

        var items = await db.Table<LocalLookupItem>()
            .Where(x => x.Group == TradeFairGroup)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Label)
            .ToListAsync();

        return items.Select(ToSelection).ToList();
    }

    public async Task<List<TradeFairSelection>> DownloadTradeFairsAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "api/mobile/config/trade-fairs");

        var token = await SecureStorage.GetAsync("auth_token");

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await _client.SendAsync(request, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"HTTP {(int)response.StatusCode}: {raw}");
        }

        var items = JsonSerializer.Deserialize<List<MobileLookupItemDto>>(
            raw,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

        var db = await _database.GetDatabaseAsync();

        await db.RunInTransactionAsync(connection =>
        {
            connection.Execute("DELETE FROM LocalLookupItem WHERE [Group] = ?", TradeFairGroup);

            foreach (var item in items)
            {
                connection.Insert(new LocalLookupItem
                {
                    Group = TradeFairGroup,
                    Key = item.Key,
                    Label = item.Label,
                    SortOrder = item.SortOrder,
                    Description = item.Description,
                    UpdatedUtc = DateTime.UtcNow
                });
            }
        });

        return items.Select(x => new TradeFairSelection
        {
            Key = x.Key,
            Label = x.Label
        }).ToList();
    }

    public async Task<List<TradeFairSelection>> EnsureTradeFairsAvailableAsync(CancellationToken cancellationToken = default)
    {
        var local = await GetLocalTradeFairsAsync();

        if (local.Count > 0)
        {
            return local;
        }

        return await DownloadTradeFairsAsync(cancellationToken);
    }

    public async Task<string> GetSelectedLabelOrFallbackAsync()
    {
        var selected = await GetSelectedAsync();
        return selected?.Label ?? "Messe auswählen";
    }

    private static TradeFairSelection ToSelection(LocalLookupItem item)
    {
        return new TradeFairSelection
        {
            Key = item.Key,
            Label = item.Label
        };
    }
}
