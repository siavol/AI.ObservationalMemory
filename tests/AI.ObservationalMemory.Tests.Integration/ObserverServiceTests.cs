using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Svl.AI.ObservationalMemory;

namespace AI.ObservationalMemory.Tests.Integration;

[MethodDataSource<AiClientDataSources>(nameof(AiClientDataSources.ChatClients))]
public class ObserverServiceTests(IChatClient chatClient)
{
    [Test]
    public async Task MakeObservations_AfterEnoughMessages(CancellationToken cancellationToken)
    {
        // TODO: reconsider using logger in library
        var logger = NullLogger<ObserverService>.Instance; 

        // Arrange
        var promptProvider = new DefaultMemoryPromptProvider();
        
        // Create ObserverService
        var observerService = new ObserverService(chatClient, promptProvider, logger); 
        
        // Create memory with 3 chat exchanges
        var memory = new UserMemory {
            RawMessages = [
                new RawMessage{
                    Timestamp = DateTimeOffset.UtcNow,
                    UserMessage = "What's your favorite programming language?",
                    AssistantResponse = "I enjoy helping with many languages, but C# and Python are particularly powerful."
                },
                new RawMessage{
                    Timestamp = DateTimeOffset.UtcNow,
                    UserMessage = "I'm working on machine learning projects.",
                    AssistantResponse = "That's exciting! Machine learning opens up many possibilities."
                },
                new RawMessage{
                    Timestamp = DateTimeOffset.UtcNow,
                    UserMessage = "I prefer using Python for data science.",
                    AssistantResponse = "Python is an excellent choice for data science with its rich ecosystem of libraries."
                }
            ]
        };
        var rawMessageCountBeforeCompress = memory.RawMessages.Count;
        
        // Act
        // Trigger observer compression
        memory = await observerService.ObserveAsync(memory, cancellationToken);
        
        await Assert.That(memory).IsNotNull();
        await Assert.That(memory.Observations).Count().IsGreaterThan(0);
        await Assert.That(memory.RawMessages).Count().IsLessThan(rawMessageCountBeforeCompress);
        
        // Assert
        var firstObservation = memory.Observations[0];
        await Assert.That(firstObservation)
            .Member(o => o.Text, text => text.IsNotEmpty())
            .And.Member(o => o.Importance, importance => importance.IsNotEmpty())
            .And.Member(o => o.Timestamp, timestamp => timestamp.IsNotEqualTo(default));
        
        // Verify at least one observation mentions relevant topics from the conversation
        var allObservationText = string.Join(" ", memory.Observations.Select(o => o.Text));
        var containsRelevantTopic = allObservationText.Contains("Python", StringComparison.OrdinalIgnoreCase) ||
                                     allObservationText.Contains("machine learning", StringComparison.OrdinalIgnoreCase) ||
                                     allObservationText.Contains("data science", StringComparison.OrdinalIgnoreCase) ||
                                     allObservationText.Contains("programming", StringComparison.OrdinalIgnoreCase);
        
        await Assert.That(containsRelevantTopic).IsTrue();
    }
}
