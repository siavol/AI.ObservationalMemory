namespace Svl.AI.ObservationalMemory;

/// <summary>
/// Compresses raw messages into durable observations using an LLM call.
/// Triggered when raw message count exceeds the configured threshold.
/// </summary>
public interface IObserverService
{
    /// <summary>
    /// Processes raw messages in the given memory, extracting observations and clearing raw messages on success.
    /// </summary>
    Task<UserMemory> ObserveAsync(UserMemory memory, CancellationToken cancellationToken = default);
}
