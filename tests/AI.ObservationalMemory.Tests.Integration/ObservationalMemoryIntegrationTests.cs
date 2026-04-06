using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using Svl.AI.ObservationalMemory;

namespace AI.ObservationalMemory.Tests.Integration;

public class ObservationalMemoryIntegrationTests
{
    [Test]
    [Timeout(60000)]
    public async Task StoreChatObservations_AfterThreeExchanges_CreatesObservations(CancellationToken cancellationToken)
    {
        // Arrange - Setup services with low threshold
        var builder = Host.CreateApplicationBuilder();
        
        // Add configuration from both user secrets and environment variables
        builder.Configuration.AddUserSecrets<ObservationalMemoryIntegrationTests>();
        builder.Configuration.AddEnvironmentVariables();
        
        // Configure logging (suppress most logs during tests)
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        
        // Configure observational memory settings with low threshold for testing
        builder.Services.AddSingleton(new ObservationalMemorySettings
        {
            ObserverRawMessageThreshold = 3, // Trigger after 3 messages
            ReflectorObservationThreshold = 100 // High enough to not trigger during test
        });
        builder.Services.AddSingleton<IOptions<ObservationalMemorySettings>>(sp =>
            Options.Create(sp.GetRequiredService<ObservationalMemorySettings>()));
        
        // Get API key from environment variable (works for both local and CI)
        var apiKey = builder.Configuration["OpenAI:ApiKey"] 
                     ?? Environment.GetEnvironmentVariable("OPENAI_APIKEY")
                     ?? throw new InvalidOperationException(
                         "OpenAI API key not configured. " +
                         "For local testing: dotnet user-secrets set \"OpenAI:ApiKey\" \"your-key\" --project tests/AI.ObservationalMemory.Tests.Integration " +
                         "For CI: Set OPENAI_APIKEY environment variable");
        
        // Register OpenAI chat client
        builder.Services.AddSingleton<IChatClient>(sp =>
        {
            var openAiClient = new OpenAIClient(apiKey);
            return openAiClient.GetChatClient("gpt-4o-mini").AsIChatClient();
        });
        
        // Register observational memory services with in-memory store
        var memoryStore = new InMemoryObservationalMemoryStore();
        builder.Services.AddSingleton<IObservationalMemoryStore>(memoryStore);
        builder.Services.AddSingleton<IMemoryPromptProvider, DefaultMemoryPromptProvider>();
        builder.Services.AddSingleton<ObservationalMemoryContext>();
        builder.Services.AddSingleton<IObserverService, ObserverService>();
        builder.Services.AddSingleton<IReflectorService, ReflectorService>();
        
        var host = builder.Build();
        var observerService = host.Services.GetRequiredService<IObserverService>();
        var memoryContext = host.Services.GetRequiredService<ObservationalMemoryContext>();
        
        const string testUserId = "test-user-integration";
        
        // Load initial memory and set context
        var memory = await memoryStore.LoadAsync(testUserId, cancellationToken);
        memoryContext.Current = memory;
        
        // Act - Simulate 3 chat exchanges
        var exchanges = new[]
        {
            ("What's your favorite programming language?", "I enjoy helping with many languages, but C# and Python are particularly powerful."),
            ("I'm working on machine learning projects.", "That's exciting! Machine learning opens up many possibilities."),
            ("I prefer using Python for data science.", "Python is an excellent choice for data science with its rich ecosystem of libraries.")
        };
        
        foreach (var (userMessage, assistantResponse) in exchanges)
        {
            // Add the exchange as a raw message
            memory.RawMessages.Add(new RawMessage
            {
                Timestamp = DateTimeOffset.UtcNow,
                UserMessage = userMessage,
                AssistantResponse = assistantResponse
            });
            await memoryStore.SaveAsync(testUserId, memory, cancellationToken);
        }
        
        // Get raw message count before compression
        var rawMessageCountBeforeCompress = memory.RawMessages.Count;
        
        // Trigger observer compression (should happen after 3 messages due to threshold)
        memory = await observerService.ObserveAsync(memory, cancellationToken);
        memoryContext.Current = memory;
        await memoryStore.SaveAsync(testUserId, memory, cancellationToken);
        
        // Assert - Verify observations were created
        var storedMemory = memoryStore.GetStoredMemory(testUserId);
        
        await Assert.That(storedMemory).IsNotNull();
        await Assert.That(storedMemory!.Observations.Count).IsGreaterThan(0);
        await Assert.That(storedMemory.RawMessages.Count).IsLessThan(rawMessageCountBeforeCompress);
        
        // Verify observation structure
        var firstObservation = storedMemory.Observations[0];
        await Assert.That(firstObservation.Text).IsNotEmpty();
        await Assert.That(firstObservation.Importance).IsNotEmpty();
        await Assert.That(firstObservation.Timestamp).IsNotEqualTo(default(DateTimeOffset));
        
        // Verify at least one observation mentions relevant topics from the conversation
        var allObservationText = string.Join(" ", storedMemory.Observations.Select(o => o.Text));
        var containsRelevantTopic = allObservationText.Contains("Python", StringComparison.OrdinalIgnoreCase) ||
                                     allObservationText.Contains("machine learning", StringComparison.OrdinalIgnoreCase) ||
                                     allObservationText.Contains("data science", StringComparison.OrdinalIgnoreCase) ||
                                     allObservationText.Contains("programming", StringComparison.OrdinalIgnoreCase);
        
        await Assert.That(containsRelevantTopic).IsTrue();
        
        Console.WriteLine($"✓ Successfully created {storedMemory.Observations.Count} observation(s):");
        foreach (var obs in storedMemory.Observations)
        {
            Console.WriteLine($"  [{obs.Importance}] {obs.Text}");
        }
    }
}
