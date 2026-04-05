using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiObservationalMemory;

/// <summary>
/// File system-based implementation of <see cref="IObservationalMemoryStore"/>.
/// Stores observations and raw messages in plain text markdown files.
/// </summary>
public sealed class FileSystemObservationalMemoryStore : IObservationalMemoryStore
{
    private readonly FileSystemObservationalMemoryStoreOptions _options;
    private readonly ILogger<FileSystemObservationalMemoryStore> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss";

    public FileSystemObservationalMemoryStore(
        IOptions<FileSystemObservationalMemoryStoreOptions> options,
        ILogger<FileSystemObservationalMemoryStore> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<UserMemory> LoadAsync(string userId, CancellationToken cancellationToken = default)
    {
        using var activity = MemoryActivitySource.Source.StartActivity(MemoryActivitySource.SpanNames.Load);
        activity?.SetTag(MemoryActivitySource.TagKeys.UserId, userId);

        try
        {
            var userDir = GetUserDirectory(userId);
            var observationsPath = Path.Combine(userDir, _options.ObservationsFileName);
            var rawMessagesPath = Path.Combine(userDir, _options.RawMessagesFileName);

            var observations = new List<Observation>();
            var rawMessages = new List<RawMessage>();

            // Load observations
            if (File.Exists(observationsPath))
            {
                var observationsText = await File.ReadAllTextAsync(observationsPath, cancellationToken);
                observations = ParseObservations(observationsText);
            }

            // Load raw messages
            if (File.Exists(rawMessagesPath))
            {
                var rawMessagesText = await File.ReadAllTextAsync(rawMessagesPath, cancellationToken);
                rawMessages = ParseRawMessages(rawMessagesText);
            }

            var memory = new UserMemory
            {
                Observations = observations,
                RawMessages = rawMessages
            };

            activity?.SetTag(MemoryActivitySource.TagKeys.ObservationsCount, observations.Count);
            activity?.SetTag(MemoryActivitySource.TagKeys.RawMessagesCount, rawMessages.Count);

            _logger.LogDebug(
                "Loaded memory for user {UserId}: {ObservationCount} observations, {RawMessageCount} raw messages",
                userId, observations.Count, rawMessages.Count);

            return memory;
        }
        catch (Exception ex)
        {
            activity?.SetException(ex);
            _logger.LogError(ex, "Failed to load memory for user {UserId}", userId);
            return new UserMemory();
        }
    }

    public async Task SaveAsync(string userId, UserMemory memory, CancellationToken cancellationToken = default)
    {
        using var activity = MemoryActivitySource.Source.StartActivity(MemoryActivitySource.SpanNames.Save);
        activity?.SetTag(MemoryActivitySource.TagKeys.UserId, userId);
        activity?.SetTag(MemoryActivitySource.TagKeys.ObservationsCount, memory.Observations.Count);
        activity?.SetTag(MemoryActivitySource.TagKeys.RawMessagesCount, memory.RawMessages.Count);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var userDir = GetUserDirectory(userId);
            Directory.CreateDirectory(userDir);

            var observationsPath = Path.Combine(userDir, _options.ObservationsFileName);
            var rawMessagesPath = Path.Combine(userDir, _options.RawMessagesFileName);

            // Write observations
            var observationsText = FormatObservations(memory.Observations);
            await File.WriteAllTextAsync(observationsPath, observationsText, cancellationToken);

            // Write raw messages
            var rawMessagesText = FormatRawMessages(memory.RawMessages);
            await File.WriteAllTextAsync(rawMessagesPath, rawMessagesText, cancellationToken);

            _logger.LogDebug(
                "Saved memory for user {UserId}: {ObservationCount} observations, {RawMessageCount} raw messages",
                userId, memory.Observations.Count, memory.RawMessages.Count);
        }
        catch (Exception ex)
        {
            activity?.SetException(ex);
            _logger.LogError(ex, "Failed to save memory for user {UserId}", userId);
            throw;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task DeleteAsync(string userId, CancellationToken cancellationToken = default)
    {
        using var activity = MemoryActivitySource.Source.StartActivity(MemoryActivitySource.SpanNames.Delete);
        activity?.SetTag(MemoryActivitySource.TagKeys.UserId, userId);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var userDir = GetUserDirectory(userId);
            if (Directory.Exists(userDir))
            {
                Directory.Delete(userDir, recursive: true);
                _logger.LogInformation("Deleted memory directory for user {UserId}", userId);
            }
        }
        catch (Exception ex)
        {
            activity?.SetException(ex);
            _logger.LogError(ex, "Failed to delete memory for user {UserId}", userId);
            throw;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private string GetUserDirectory(string userId)
    {
        // Sanitize userId to be filesystem-safe
        var safeUserId = string.Join("_", userId.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_options.BaseDirectory, safeUserId);
    }

    private static List<Observation> ParseObservations(string text)
    {
        var observations = new List<Observation>();
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;

            // Format: 🔴 [2024-03-14 10:30:00] User prefers vegetarian food
            var importance = line.Length > 0 && char.IsHighSurrogate(line[0]) 
                ? line.Substring(0, 2).Trim() 
                : "🟡";

            var bracketStart = line.IndexOf('[');
            var bracketEnd = line.IndexOf(']');

            if (bracketStart == -1 || bracketEnd == -1)
                continue;

            var timestampStr = line.Substring(bracketStart + 1, bracketEnd - bracketStart - 1).Trim();
            var observationText = line.Substring(bracketEnd + 1).Trim();

            if (DateTimeOffset.TryParseExact(timestampStr, TimestampFormat, 
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp))
            {
                observations.Add(new Observation
                {
                    Timestamp = timestamp,
                    Importance = importance,
                    Text = observationText
                });
            }
        }

        return observations;
    }

    private static List<RawMessage> ParseRawMessages(string text)
    {
        var rawMessages = new List<RawMessage>();
        var blocks = text.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var block in blocks)
        {
            var lines = block.Split('\n', StringSplitOptions.TrimEntries);
            if (lines.Length < 3)
                continue;

            // Format:
            // [2024-03-14 10:30:00]
            // User: Hello!
            // Assistant: Hi there!

            var timestampLine = lines[0].Trim('[', ']');
            if (!DateTimeOffset.TryParseExact(timestampLine, TimestampFormat,
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp))
                continue;

            string? userMessage = null;
            string? assistantResponse = null;

            for (int i = 1; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("User:", StringComparison.OrdinalIgnoreCase))
                {
                    userMessage = lines[i].Substring(5).Trim();
                }
                else if (lines[i].StartsWith("Assistant:", StringComparison.OrdinalIgnoreCase))
                {
                    assistantResponse = lines[i].Substring(10).Trim();
                }
            }

            if (!string.IsNullOrEmpty(userMessage) && !string.IsNullOrEmpty(assistantResponse))
            {
                rawMessages.Add(new RawMessage
                {
                    Timestamp = timestamp,
                    UserMessage = userMessage,
                    AssistantResponse = assistantResponse
                });
            }
        }

        return rawMessages;
    }

    private static string FormatObservations(IReadOnlyList<Observation> observations)
    {
        if (observations.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("# Observations");
        sb.AppendLine();

        foreach (var obs in observations)
        {
            var timestamp = obs.Timestamp.ToString(TimestampFormat);
            sb.AppendLine($"{obs.Importance} [{timestamp}] {obs.Text}");
        }

        return sb.ToString();
    }

    private static string FormatRawMessages(IReadOnlyList<RawMessage> rawMessages)
    {
        if (rawMessages.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("# Raw Message Exchanges");
        sb.AppendLine();

        for (int i = 0; i < rawMessages.Count; i++)
        {
            var msg = rawMessages[i];
            var timestamp = msg.Timestamp.ToString(TimestampFormat);

            sb.AppendLine($"[{timestamp}]");
            sb.AppendLine($"User: {msg.UserMessage}");
            sb.AppendLine($"Assistant: {msg.AssistantResponse}");

            if (i < rawMessages.Count - 1)
                sb.AppendLine();
        }

        return sb.ToString();
    }
}
