using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Svl.AI.ObservationalMemory;

/// <summary>
/// Observer service that compresses raw messages into durable observations using an LLM call.
/// Wraps the entire operation in a <c>Memory.Observe</c> OTEL span.
/// </summary>
public sealed class ObserverService : IObserverService
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<ObserverService> _logger;
    private readonly string _observerPrompt;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ObserverService(
        IChatClient chatClient,
        IMemoryPromptProvider promptProvider,
        ILogger<ObserverService> logger)
    {
        _chatClient = chatClient;
        _logger = logger;
        _observerPrompt = promptProvider.ObserverPrompt;
    }

    public async Task<UserMemory> ObserveAsync(UserMemory memory, CancellationToken cancellationToken = default)
    {
        using var activity = MemoryActivitySource.Source.StartActivity(MemoryActivitySource.SpanNames.Observe);
        activity?.SetTag("memory.observer.input_raw_messages", memory.RawMessages.Count);
        activity?.SetTag("memory.observer.input_observations", memory.Observations.Count);

        try
        {
            var userMessage = BuildUserMessage(memory);

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, _observerPrompt),
                new(ChatRole.User, userMessage)
            };

            var response = await _chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
            var responseText = response.Text?.Trim() ?? string.Empty;

            var newObservations = ParseObservations(responseText);

            _logger.LogInformation(
                "Observer extracted {Count} observations from {RawCount} raw messages",
                newObservations.Count,
                memory.RawMessages.Count);

            // Merge: keep existing observations + add new ones, clear raw messages
            var mergedObservations = new List<Observation>(memory.Observations);
            mergedObservations.AddRange(newObservations);

            var updatedMemory = memory with
            {
                Observations = mergedObservations,
                RawMessages = []
            };

            activity?.SetTag("memory.observer.output_observations", updatedMemory.Observations.Count);
            activity?.SetTag(MemoryActivitySource.TagKeys.ObservationsCount, updatedMemory.Observations.Count);
            activity?.SetTag(MemoryActivitySource.TagKeys.RawMessagesCount, 0);

            return updatedMemory;
        }
        catch (Exception ex)
        {
            activity?.SetException(ex);
            _logger.LogWarning(ex, "Observer LLM call failed; raw messages preserved");
            throw;
        }
    }

    private static string BuildUserMessage(UserMemory memory)
    {
        var sb = new System.Text.StringBuilder();

        if (memory.Observations.Count > 0)
        {
            sb.AppendLine("## Existing Observations");
            foreach (var obs in memory.Observations)
            {
                sb.AppendLine($"- {obs.Importance} {obs.Text}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("## Recent Conversation Exchanges");
        foreach (var msg in memory.RawMessages)
        {
            sb.AppendLine($"**User**: {msg.UserMessage}");
            sb.AppendLine($"**Assistant**: {msg.AssistantResponse}");
            sb.AppendLine();
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

        var parsed = JsonSerializer.Deserialize<List<ObserverOutput>>(json, JsonOptions);
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

    private sealed record ObserverOutput
    {
        public string? Text { get; init; }
        public string? Importance { get; init; }
    }
}
