namespace ReadingQueue.Infrastructure.LLM;

public sealed class ClaudeOptions
{
    public string ApiKey         { get; set; } = string.Empty;
    public string Model          { get; set; } = "claude-haiku-4-5-20251001";
    public int    MaxTokens      { get; set; } = 1024;
    public int    TimeoutSeconds { get; set; } = 30;
    public int    MaxRetries     { get; set; } = 3;
    public string BaseUrl        { get; set; } = string.Empty; // solo para tests con WireMock
}
