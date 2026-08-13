using MesseLeads.Mobile.Models;

namespace MesseLeads.Mobile.Services;

public interface ILocalAiService
{
    Task<BusinessCardAiResult> ReadBusinessCardAsync(
        string? ocrText,
        CancellationToken cancellationToken = default);
}