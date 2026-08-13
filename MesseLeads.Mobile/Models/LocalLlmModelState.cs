namespace MesseLeads.Mobile.Models;

public sealed class LocalLlmModelState
{
    public bool IsInstalled { get; init; }
    public string ModelPath { get; init; } = "";
    public long SizeBytes { get; init; }
    public string StatusText { get; init; } = "";
}
