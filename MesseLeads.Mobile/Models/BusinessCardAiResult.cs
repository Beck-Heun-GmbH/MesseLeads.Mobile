namespace MesseLeads.Mobile.Models;

public sealed class BusinessCardAiResult
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Company { get; set; }
    public string? JobTitle { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Mobile { get; set; }
    public string? Website { get; set; }
    public string? Street { get; set; }
    public string? ZipCode { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }

    public string RawText { get; set; } = "";
    public string Engine { get; set; } = "Heuristic";

    // Diagnosewerte der lokalen KI-Pipeline.
    public bool NativeRuntimeAvailable { get; set; }
    public bool UsedFallback { get; set; }
    public string? ModelStatus { get; set; }
    public long ModelSizeBytes { get; set; }
    public string? ModelPath { get; set; }
    public int PromptLength { get; set; }
    public string GeneratedText { get; set; } = "";
    public string ParsedJson { get; set; } = "";
    public string? DiagnosticError { get; set; }
    public long OcrDurationMs { get; set; }
    public long ModelPreparationDurationMs { get; set; }
    public long InferenceDurationMs { get; set; }
    public long JsonParsingDurationMs { get; set; }
    public long TotalDurationMs { get; set; }
}
