namespace MesseLeads.Mobile.Models;

public sealed class DraftListItem
{
    public Guid LocalId { get; set; }

    public string Title { get; set; } = "";

    public string Subtitle { get; set; } = "";

    public string StatusText { get; set; } = "";

    public string UpdatedText { get; set; } = "";
}