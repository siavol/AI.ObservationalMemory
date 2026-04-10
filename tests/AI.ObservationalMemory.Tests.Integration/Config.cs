using Microsoft.Extensions.Configuration;

namespace AI.ObservationalMemory.Tests.Integration;

public static class Config
{
    public static IConfiguration Configuration { get; } = new ConfigurationBuilder()
            .AddUserSecrets<ObserverServiceTests>()
            .AddEnvironmentVariables()
            .Build();

    public static string OpenAiApiKey => Configuration["OpenAI:ApiKey"] 
        ?? throw new InvalidOperationException("OpenAI API key not found in configuration.");

    public static string OpenAiModel => Configuration["OpenAI:Model"] ?? "gpt-4o-mini";
}