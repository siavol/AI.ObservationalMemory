using System.Collections.Concurrent;
using Svl.AI.ObservationalMemory;

namespace AI.ObservationalMemory.Tests.Integration;

/// <summary>
/// In-memory implementation of <see cref="IObservationalMemoryStore"/> for testing.
/// </summary>
internal sealed class InMemoryObservationalMemoryStore : IObservationalMemoryStore
{
    private readonly ConcurrentDictionary<string, UserMemory> _storage = new();

    public Task<UserMemory> LoadAsync(string userId, CancellationToken cancellationToken = default)
    {
        var memory = _storage.GetOrAdd(userId, _ => new UserMemory());
        return Task.FromResult(memory);
    }

    public Task SaveAsync(string userId, UserMemory memory, CancellationToken cancellationToken = default)
    {
        _storage[userId] = memory;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string userId, CancellationToken cancellationToken = default)
    {
        _storage.TryRemove(userId, out _);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the stored memory for a user. Useful for test assertions.
    /// </summary>
    public UserMemory? GetStoredMemory(string userId)
    {
        return _storage.TryGetValue(userId, out var memory) ? memory : null;
    }
}
