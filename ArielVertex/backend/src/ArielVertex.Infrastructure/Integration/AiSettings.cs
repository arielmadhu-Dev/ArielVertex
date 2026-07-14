namespace ArielVertex.Infrastructure.Integration;

/// <summary>
/// Optional AI assistance for auto-generating minutes of a meeting and grammar correction.
/// Ships <b>off</b>: the built-in generator structures + cleans the notes deterministically with no
/// external call. Supply an API key and flip <see cref="Enabled"/> to activate AI-grade minutes —
/// a config drop-in, exactly like the Microsoft/Graph integration. Keep the key in a secret store or
/// environment variable (<c>Ai__ApiKey</c>), never in source. The admin can also turn AI polishing
/// off at runtime via the <c>minutes.aiPolish</c> platform setting without touching config.
/// </summary>
public class AiSettings
{
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "Anthropic";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "claude-opus-4-8";
    public string BaseUrl { get; set; } = "https://api.anthropic.com/v1";
    public int MaxTokens { get; set; } = 1500;

    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(ApiKey);
}
