using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Svl.AI.ObservationalMemory;

/// <summary>
/// Reflector service that prunes/deduplicates observations using an LLM call.
/// Wraps the entire operation in a <c>Memory.Reflect</c> OTEL span.
/// </summary>
public sealed class ReflectorService : IReflectorService
{
    private readonly IChatClient _chatClient;
    private readonly ILogger? _logger;
    private readonly string _reflectorPrompt;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ReflectorService(
        IChatClient chatClient,
        IMemoryPromptProvider promptProvider,
        ILoggerProvider? loggerProvider = null)
    {
        _chatClient = chatClient;
        _logger = loggerProvider?.CreateLogger(typeof(ReflectorService).FullName ?? nameof(ReflectorService));
        _reflectorPrompt = promptProvider.ReflectorPrompt;
    }

    public async Task<UserMemory> ReflectAsync(UserMemory memory, CancellationToken cancellationToken = default)
    {
        using var activity = MemoryActivitySource.Source.StartActivity(MemoryActivitySource.SpanNames.Reflect);
        activity?.SetTag("memory.reflector.before_observations", memory.Observations.Count);

        try
        {
            var userMessage = BuildUserMessage(memory);

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, _reflectorPrompt),
                new(ChatRole.User, userMessage)
            };

            var response = await _chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
            var responseText = response.Text?.Trim() ?? string.Empty;

            var prunedObservations = ParseObservations(responseText);

            _logger?.LogInformation(
                "Reflector pruned observations from {Before} to {After}",
                memory.Observations.Count,
                prunedObservations.Count);

            var updatedMemory = memory with
            {
                Observations = prunedObservations
            };

            activity?.SetTag("memory.reflector.after_observations", updatedMemory.Observations.Count);
            activity?.SetTag(MemoryActivitySource.TagKeys.ObservationsCount, updatedMemory.Observations.Count);
            activity?.SetTag(MemoryActivitySource.TagKeys.RawMessagesCount, updatedMemory.RawMessages.Count);

            return updatedMemory;
        }
        catch (Exception ex)
        {
            activity?.SetException(ex);
            _logger?.LogWarning(ex, "Reflector LLM call failed; observations preserved");
            throw;
        }
    }

    private static string BuildUserMessage(UserMemory memory)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("## Current Observations to Prune/Consolidate");
        sb.AppendLine();

        foreach (var obs in memory.Observations)
        {
            sb.AppendLine($"- {obs.Importance} {obs.Text} (recorded {obs.Timestamp:yyyy-MM-dd})");
        }

        return sb.ToString();
    }

    private static List<Observation> ParseObservations(string responseText)
    {
        // Strip markdown code fences if present
        var json = responseText;
        if (json.StartsWith("```"))
        {
            var firstNewline = json.IndexOf('\n');
            if (firstNewline >= 0)
                json = json[(firstNewline + 1)..];
            var lastFence = json.LastIndexOf("```");
            if (lastFence >= 0)
                json = json[..lastFence];
            json = json.Trim();
        }

        var parsed = JsonSerializer.Deserialize<List<ReflectorOutput>>(json, JsonOptions);
        if (parsed == null || parsed.Count == 0)
            return [];

        var now = DateTimeOffset.UtcNow;
        return parsed.Select(o => new Observation
        {
            Timestamp = now,
            Text = o.Text ?? string.Empty,
            Importance = o.Importance ?? "🟢"
        }).ToList();
    }

    private sealed record ReflectorOutput
    {
        public string? Text { get; init; }
        public string? Importance { get; init; }
    }
}
