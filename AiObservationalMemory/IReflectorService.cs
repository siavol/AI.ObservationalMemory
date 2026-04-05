namespace AiObservationalMemory;

/// <summary>
/// Prunes and deduplicates observations using an LLM call when the observation count exceeds the configured threshold.
/// </summary>
public interface IReflectorService
{
    /// <summary>
    /// Processes observations in the given memory, returning an updated memory with a pruned observation list.
    /// </summary>
    Task<UserMemory> ReflectAsync(UserMemory memory, CancellationToken cancellationToken = default);
}
