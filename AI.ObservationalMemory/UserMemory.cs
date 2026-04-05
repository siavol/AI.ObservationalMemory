namespace Svl.AI.ObservationalMemory;

/// <summary>
/// Per-user observational memory containing durable observations and recent unprocessed messages.
/// </summary>
public record UserMemory
{
    /// <summary>
    /// Compressed durable facts extracted by the observer LLM.
    /// </summary>
    public List<Observation> Observations { get; init; } = [];

    /// <summary>
    /// Recent user/assistant turns not yet processed by the observer.
    /// </summary>
    public List<RawMessage> RawMessages { get; init; } = [];
}

/// <summary>
/// A single durable observation about the user.
/// </summary>
public record Observation
{
    public required DateTimeOffset Timestamp { get; init; }
    public required string Text { get; init; }
    public required string Importance { get; init; }
}

/// <summary>
/// A single unprocessed user/assistant exchange.
/// </summary>
public record RawMessage
{
    public required DateTimeOffset Timestamp { get; init; }
    public required string UserMessage { get; init; }
    public required string AssistantResponse { get; init; }
}
