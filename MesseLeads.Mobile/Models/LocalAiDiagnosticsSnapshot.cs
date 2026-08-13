namespace MesseLeads.Mobile.Models;

public sealed class LocalAiDiagnosticsSnapshot
{
    public bool RuntimeAvailable { get; init; }
    public bool ModelInstalled { get; init; }
    public bool PackagedModelFound { get; init; }
    public string RuntimeStatus { get; init; } = "Unbekannt";
    public string ModelStatus { get; init; } = "Unbekannt";
    public string ModelPath { get; init; } = "";
    public long ModelSizeBytes { get; init; }
}
