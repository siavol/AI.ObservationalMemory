# AiObservationalMemory

A lightweight .NET library for adding observational memory to AI chat applications. The library automatically captures conversation context, extracts durable observations using LLM calls, and provides memory persistence through pluggable storage backends.

## Features

- **Automatic Memory Capture**: Raw message exchanges are captured automatically
- **LLM-Powered Compression**: Observer service extracts key observations from conversations
- **Smart Pruning**: Reflector service deduplicates and consolidates observations
- **Pluggable Storage**: Built-in implementations for file system and Redis
- **OpenTelemetry Integration**: Full instrumentation for monitoring and debugging
- **Multi-Targeted**: Supports .NET 8.0 and .NET 10.0

## Installation

```bash
dotnet add package AiObservationalMemory
```

## Quick Start

### 1. Choose a Storage Implementation

#### File System Storage (included)

```csharp
builder.Services.AddOptions<FileSystemObservationalMemoryStoreOptions>()
    .Configure(options =>
    {
        options.BaseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MyApp", "Memory");
        options.ObservationsFileName = "memory.observations.md";
        options.RawMessagesFileName = "memory.raw.md";
    });

builder.Services.AddSingleton<IObservationalMemoryStore, FileSystemObservationalMemoryStore>();
```

#### Redis Storage (requires StackExchange.Redis)

Implement `IObservationalMemoryStore` using Redis as shown in ChatBro's `RedisObservationalMemoryStore`.

### 2. Configure Memory Services

```csharp
using AiObservationalMemory;
using Microsoft.Extensions.AI;

// Register IChatClient for LLM calls (Observer/Reflector)
builder.Services.AddSingleton<IChatClient>(sp =>
{
    var openAiClient = sp.GetRequiredService<OpenAI.OpenAIClient>();
    return openAiClient.GetChatClient("gpt-4o-mini").AsIChatClient();
});

// Register memory services
builder.Services.AddOptions<ObservationalMemorySettings>()
    .Configure(options =>
    {
        options.ObserverRawMessageThreshold = 20;
        options.ReflectorObservationThreshold = 50;
    });

builder.Services.AddSingleton<IMemoryPromptProvider, DefaultMemoryPromptProvider>();
builder.Services.AddSingleton<ObservationalMemoryContext>();
builder.Services.AddSingleton<IObserverService, ObserverService>();
builder.Services.AddSingleton<IReflectorService, ReflectorService>();
```

### 3. Integrate into Chat Loop

```csharp
public class ChatService
{
    private readonly IObservationalMemoryStore _memoryStore;
    private readonly ObservationalMemoryContext _memoryContext;
    private readonly IObserverService _observerService;
    private readonly IReflectorService _reflectorService;
    private readonly ObservationalMemorySettings _settings;

    public async Task<string> GetChatResponseAsync(string message, string userId)
    {
        // Load memory and set context
        var memory = await _memoryStore.LoadAsync(userId);
        _memoryContext.Current = memory;

        try
        {
            // Get AI response (memory is available via _memoryContext.Current)
            var response = await GetAIResponseWithMemory(message, memory);

            // Capture raw message exchange
            memory ??= new UserMemory();
            memory.RawMessages.Add(new RawMessage
            {
                Timestamp = DateTimeOffset.UtcNow,
                UserMessage = message,
                AssistantResponse = response
            });
            await _memoryStore.SaveAsync(userId, memory);

            // Trigger observer if threshold reached
            if (memory.RawMessages.Count >= _settings.ObserverRawMessageThreshold)
            {
                memory = await _observerService.ObserveAsync(memory);
                await _memoryStore.SaveAsync(userId, memory);

                // Trigger reflector if threshold reached
                if (memory.Observations.Count >= _settings.ReflectorObservationThreshold)
                {
                    memory = await _reflectorService.ReflectAsync(memory);
                    await _memoryStore.SaveAsync(userId, memory);
                }
            }

            return response;
        }
        finally
        {
            _memoryContext.Current = null;
        }
    }
}
```

## Storage Implementations

### File System Storage

The `FileSystemObservationalMemoryStore` saves memory to human-readable markdown files:

**Directory Structure:**
```
{BaseDirectory}/
  {userId}/
    memory.observations.md
    memory.raw.md
```

**Example observations.md:**
```markdown
# Observations

🔴 [2024-03-14 10:30:00] User prefers vegetarian food
🟡 [2024-03-14 10:35:00] Lives in Tampere, Finland
🟢 [2024-03-14 10:40:00] Enjoys hiking on weekends
```

**Example raw.md:**
```markdown
# Raw Message Exchanges

[2024-03-14 10:30:00]
User: Hello! My name is Alex and I am a vegetarian.
Assistant: Hey Alex! Nice to meet you...

[2024-03-14 10:35:00]
User: I live in Tampere.
Assistant: Cool! Tampere is a great city...
```

**Configuration Options:**
- `BaseDirectory`: Where user directories are created (default: LocalApplicationData/ObservationalMemory)
- `ObservationsFileName`: Name of observations file (default: memory.observations.md)
- `RawMessagesFileName`: Name of raw messages file (default: memory.raw.md)

### Custom Storage

Implement `IObservationalMemoryStore` for your own storage backend:

```csharp
public interface IObservationalMemoryStore
{
    Task<UserMemory> LoadAsync(string userId, CancellationToken cancellationToken = default);
    Task SaveAsync(string userId, UserMemory memory, CancellationToken cancellationToken = default);
    Task DeleteAsync(string userId, CancellationToken cancellationToken = default);
}
```

## Architecture

### Core Components

- **UserMemory**: Contains Observations (durable facts) and RawMessages (temporary exchanges)
- **ObserverService**: Compresses raw messages into observations using LLM
- **ReflectorService**: Prunes and deduplicates observations when threshold is reached
- **ObservationalMemoryContext**: AsyncLocal carrier for per-request memory flow

### Memory Flow

1. **Capture**: Raw messages accumulate during conversations
2. **Observe**: When threshold is reached, Observer extracts key observations
3. **Reflect**: When observations exceed threshold, Reflector consolidates them
4. **Context**: Memory flows from scoped services to singleton providers via AsyncLocal

### Importance Markers

Observations are tagged with importance levels:
- 🔴 High importance (critical user preferences, facts)
- 🟡 Medium importance (contextual details)
- 🟢 Low importance (transient information)

## OpenTelemetry Instrumentation

All operations emit spans under ActivitySource `AiObservationalMemory`:

**Spans:**
- `Memory.Load` - Loading user memory
- `Memory.Save` - Saving user memory
- `Memory.Delete` - Deleting user memory
- `Memory.Observe` - Observer compression
- `Memory.Reflect` - Reflector pruning

**Tags:**
- `memory.user_id` - User identifier
- `memory.observations.count` - Number of observations
- `memory.raw_messages.count` - Number of raw messages

## Custom Prompts

Provide custom prompts by implementing `IMemoryPromptProvider`:

```csharp
public class CustomMemoryPromptProvider : IMemoryPromptProvider
{
    public string ObserverPrompt => "Your custom observer prompt...";
    public string ReflectorPrompt => "Your custom reflector prompt...";
}

// Register
builder.Services.AddSingleton<IMemoryPromptProvider, CustomMemoryPromptProvider>();
```

## Running Integration Tests

The integration tests validate the library's end-to-end functionality by making real OpenAI API calls. To run them, you need to configure an OpenAI API key.

Set up your OpenAI API key using .NET user secrets:

```shell
cd tests/AI.ObservationalMemory.Tests.Integration
dotnet user-secrets set "OpenAI:ApiKey" "sk-your-key-here"
```

Then run the tests:

```shell
dotnet test
```

**Note**: Integration tests use the `gpt-4o-mini` model and make real API calls, which incur small costs. The tests use a low threshold (3 messages) to minimize token usage.

## License

MIT License - see LICENSE file for details.
