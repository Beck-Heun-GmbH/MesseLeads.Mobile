namespace MesseLeads.Mobile.Models;

public sealed class TradeFairSelection
{
    public string Key { get; set; } = "";

    public string Label { get; set; } = "";

    public string DisplayName => string.IsNullOrWhiteSpace(Key)
        ? Label
        : $"{Label} ({Key})";
}
