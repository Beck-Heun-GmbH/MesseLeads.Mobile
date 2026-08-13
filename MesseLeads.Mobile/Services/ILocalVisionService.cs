using MesseLeads.Mobile.Models;

namespace MesseLeads.Mobile.Services;

public interface ILocalVisionService
{
    Task<string> ReadBusinessCardAsync(
        string imagePath,
        CancellationToken cancellationToken = default);
}