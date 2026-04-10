namespace Svl.AI.ObservationalMemory;

/// <summary>
/// Provides system prompts for observer and reflector LLM operations.
/// </summary>
public interface IMemoryPromptProvider
{
    /// <summary>
    /// System prompt for the observer service that extracts observations from raw messages.
    /// </summary>
    string ObserverPrompt { get; }

    /// <summary>
    /// System prompt for the reflector service that prunes/deduplicates observations.
    /// </summary>
    string ReflectorPrompt { get; }
}
