namespace MesseLeads.Mobile.Models;

public sealed class LocalLlmOptions
{
    public const string ModelFileName = "qwen2.5-1.5b-instruct-q4_k_m.gguf";
    public const string PackagedModelPath = "Models/" + ModelFileName;

    public int ContextSize { get; init; } = 2048;
    public int MaxOutputTokens { get; init; } = 384;
    public int CpuThreadCount { get; init; } = Math.Max(2, Environment.ProcessorCount - 1);
    public float Temperature { get; init; } = 0.0f;
    public float TopP { get; init; } = 0.90f;
}
