namespace MesseLeads.Mobile.Dtos;

public sealed class MobileImageUploadResponse
{
    public bool Success { get; set; }
    public int? ServerImageId { get; set; }
    public string? Error { get; set; }
}