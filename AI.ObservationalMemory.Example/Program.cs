using AI.ObservationalMemory.Example;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenAI;
using Svl.AI.ObservationalMemory;

var builder = Host.CreateApplicationBuilder(args);

// Add user secrets for API key
builder.Configuration.AddUserSecrets<Program>();

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Warning);

// Configure file system storage
builder.Services.AddSingleton(new FileSystemObservationalMemoryStoreOptions
{
    BaseDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AiObservationalMemory.Example"),
    ObservationsFileName = "memory.observations.md",
    RawMessagesFileName = "memory.raw.md"
});

// Configure observational memory settings
builder.Services.AddSingleton(new ObservationalMemorySettings
{
    // use low thresholds for demo purposes to trigger observer/reflector more frequently
    ObserverRawMessageThreshold = 5,
    ReflectorObservationThreshold = 15 
});

// Register OpenAI client
var apiKey = builder.Configuration["OpenAI:ApiKey"] 
    ?? throw new InvalidOperationException("OpenAI:ApiKey not configured. Run: dotnet user-secrets set \"OpenAI:ApiKey\" \"your-key\"");

// Register IChatClient for observational memory services
builder.Services.AddSingleton<IChatClient>(sp =>
{
    var openAiClient = new OpenAIClient(apiKey);
    return new ChatClientBuilder(openAiClient.GetChatClient("gpt-4o-mini").AsIChatClient())
        .Build();
});

// Register observational memory services
builder.Services.AddSingleton<IMemoryPromptProvider, DefaultMemoryPromptProvider>();
builder.Services.AddSingleton<IObservationalMemoryStore, FileSystemObservationalMemoryStore>();
builder.Services.AddSingleton<ObservationalMemoryContext>();
builder.Services.AddSingleton<IObserverService, ObserverService>();
builder.Services.AddSingleton<IReflectorService, ReflectorService>();

// Register application services
builder.Services.AddSingleton<ChatApplication>();

var host = builder.Build();

// Run the chat application
var app = host.Services.GetRequiredService<ChatApplication>();
await app.RunAsync();
