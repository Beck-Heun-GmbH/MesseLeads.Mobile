using MesseLeads.Mobile.Data;
using MesseLeads.Mobile.Models;

namespace MesseLeads.Mobile.Services;

public sealed class LocalLeadService
{
    private readonly LocalDatabaseService _databaseService;
    private readonly TradeFairSelectionService _tradeFairSelectionService;

    public LocalLeadService(
        LocalDatabaseService databaseService,
        TradeFairSelectionService tradeFairSelectionService)
    {
        _databaseService = databaseService;
        _tradeFairSelectionService = tradeFairSelectionService;
    }

    public async Task<LocalLead> CreateDraftAsync()
    {
        var displayName = await SecureStorage.GetAsync("display_name");
        var tradeFair = await _tradeFairSelectionService.GetSelectedAsync();

        var lead = new LocalLead
        {
            LocalId = Guid.NewGuid(),
            SyncStatus = "Draft",
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow,
            OwnerUser = displayName,
            TradeFairKey = tradeFair?.Key,
            TradeFair = tradeFair?.Label
        };

        var db = await _databaseService.GetDatabaseAsync();
        await db.InsertAsync(lead);

        return lead;
    }

    public async Task<List<LocalLead>> GetAllAsync()
    {
        var db = await _databaseService.GetDatabaseAsync();

        return await db.Table<LocalLead>()
            .OrderByDescending(x => x.UpdatedUtc)
            .ToListAsync();
    }

    public async Task<List<LocalLead>> GetDraftsAsync()
    {
        var db = await _databaseService.GetDatabaseAsync();

        return await db.Table<LocalLead>()
            .Where(x => x.SyncStatus == "Draft" || x.SyncStatus == "PendingUpload")
            .OrderByDescending(x => x.UpdatedUtc)
            .ToListAsync();
    }

    public async Task<List<LocalLead>> GetHistoryAsync()
    {
        var db = await _databaseService.GetDatabaseAsync();

        return await db.Table<LocalLead>()
            .Where(x => x.SyncStatus == "Synced" || x.SyncStatus == "PendingUpdate")
            .OrderByDescending(x => x.UpdatedUtc)
            .ToListAsync();
    }

    public async Task<LocalLead?> GetAsync(Guid localId)
    {
        var db = await _databaseService.GetDatabaseAsync();

        return await db.Table<LocalLead>()
            .FirstOrDefaultAsync(x => x.LocalId == localId);
    }

    public async Task SaveAsync(LocalLead lead)
    {
        lead.UpdatedUtc = DateTime.UtcNow;

        if (lead.SyncStatus == "Synced")
        {
            lead.SyncStatus = "PendingUpdate";
        }

        var db = await _databaseService.GetDatabaseAsync();
        await db.UpdateAsync(lead);
    }

    public async Task DeleteAsync(LocalLead lead)
    {
        var db = await _databaseService.GetDatabaseAsync();

        var images = await db.Table<LocalLeadImage>()
            .Where(x => x.LeadLocalId == lead.LocalId)
            .ToListAsync();

        foreach (var image in images)
        {
            if (!string.IsNullOrWhiteSpace(image.LocalFilePath) && File.Exists(image.LocalFilePath))
            {
                File.Delete(image.LocalFilePath);
            }

            await db.DeleteAsync(image);
        }

        await db.DeleteAsync(lead);
    }

    public async Task<List<LocalLead>> GetPendingSyncAsync()
    {
        var db = await _databaseService.GetDatabaseAsync();

        return await db.Table<LocalLead>()
            .Where(x => x.SyncStatus == "PendingUpload" || x.SyncStatus == "PendingUpdate")
            .OrderBy(x => x.UpdatedUtc)
            .ToListAsync();
    }

    public async Task MarkSyncedAsync(LocalLead lead, int serverId)
    {
        lead.ServerId = serverId;
        lead.SyncStatus = "Synced";
        lead.LastSyncError = null;
        lead.LastSyncedUtc = DateTime.UtcNow;
        lead.UpdatedUtc = DateTime.UtcNow;

        var db = await _databaseService.GetDatabaseAsync();
        await db.UpdateAsync(lead);
    }

    public async Task MarkSyncFailedAsync(LocalLead lead, string error)
    {
        lead.SyncRetryCount++;
        lead.LastSyncError = error;
        lead.UpdatedUtc = DateTime.UtcNow;

        var db = await _databaseService.GetDatabaseAsync();
        await db.UpdateAsync(lead);
    }

    public async Task<int> CountDraftsAsync()
    {
        var db = await _databaseService.GetDatabaseAsync();

        return await db.Table<LocalLead>()
            .Where(x => x.SyncStatus == "Draft" || x.SyncStatus == "PendingUpload")
            .CountAsync();
    }

    public async Task<int> CountHistoryAsync()
    {
        var db = await _databaseService.GetDatabaseAsync();

        return await db.Table<LocalLead>()
            .Where(x => x.SyncStatus == "Synced" || x.SyncStatus == "PendingUpdate")
            .CountAsync();
    }

    public async Task<int> CountPendingUploadsAsync()
    {
        var db = await _databaseService.GetDatabaseAsync();

        var pendingLeads = await db.Table<LocalLead>()
            .Where(x => x.SyncStatus == "PendingUpload" || x.SyncStatus == "PendingUpdate")
            .CountAsync();

        var pendingImages = await db.Table<LocalLeadImage>()
            .Where(x => x.SyncStatus == "Pending")
            .CountAsync();

        return pendingLeads + pendingImages;
    }
}
