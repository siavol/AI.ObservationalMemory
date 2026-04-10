using Microsoft.Extensions.AI;
using OpenAI;

namespace AI.ObservationalMemory.Tests.Integration;

public class AiClientDataSources
{
    public static IEnumerable<IChatClient> ChatClients()
    {
        yield return new OpenAIClient(Config.OpenAiApiKey)
            .GetChatClient(Config.OpenAiModel)
            .AsIChatClient();
    }
}