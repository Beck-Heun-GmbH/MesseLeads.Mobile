namespace MesseLeads.Mobile.Models;

public sealed class MobileLoginResponse
{
    public bool Success { get; set; }

    public string? Token { get; set; }

    public DateTime? ExpiresUtc { get; set; }

    public int? UserId { get; set; }

    public string? DisplayName { get; set; }

    public bool IsAdmin { get; set; }

    public bool MustChangePassword { get; set; }

    public string? Error { get; set; }
}
