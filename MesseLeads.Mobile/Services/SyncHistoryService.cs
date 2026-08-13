using MesseLeads.Mobile.Data;
using MesseLeads.Mobile.Models;

namespace MesseLeads.Mobile.Services;

public sealed class SyncHistoryService
{
    private readonly LocalDatabaseService _databaseService;

    public SyncHistoryService(LocalDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task AddAsync(
        DateTime startedUtc,
        DateTime finishedUtc,
        LeadSyncResult result,
        string trigger)
    {
        var db = await _databaseService.GetDatabaseAsync();

        await db.InsertAsync(new LocalSyncHistory
        {
            StartedUtc = startedUtc,
            FinishedUtc = finishedUtc,
            Success = result.Success,
            Trigger = trigger,
            LeadTotal = result.Total,
            LeadSynced = result.Synced,
            LeadFailed = result.Failed,
            ImageSynced = result.ImageSynced,
            ImageFailed = result.ImageFailed,
            Message = result.Message
        });
    }

    public async Task<List<LocalSyncHistory>> GetRecentAsync(int take = 25)
    {
        var db = await _databaseService.GetDatabaseAsync();

        return await db.Table<LocalSyncHistory>()
            .OrderByDescending(x => x.StartedUtc)
            .Take(take)
            .ToListAsync();
    }
}
