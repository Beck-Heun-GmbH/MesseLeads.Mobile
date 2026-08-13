namespace MesseLeads.Mobile.Dtos;

public sealed class MobileLeadUploadResponse
{
    public bool Success { get; set; }
    public int? ServerId { get; set; }
    public string? TradeFairKey { get; set; }
    public string? TradeFair { get; set; }
    public string? Error { get; set; }
}
