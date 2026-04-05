namespace AiObservationalMemory;

/// <summary>
/// Persistence interface for per-user observational memory.
/// </summary>
public interface IObservationalMemoryStore
{
    /// <summary>
    /// Loads the user's memory from the store. Returns a new empty <see cref="UserMemory"/> if none exists.
    /// </summary>
    Task<UserMemory> LoadAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the user's memory to the store.
    /// </summary>
    Task SaveAsync(string userId, UserMemory memory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the user's memory from the store.
    /// </summary>
    Task DeleteAsync(string userId, CancellationToken cancellationToken = default);
}
