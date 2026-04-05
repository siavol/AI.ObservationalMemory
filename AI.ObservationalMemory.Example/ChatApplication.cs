using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;
using Svl.AI.ObservationalMemory;

namespace AI.ObservationalMemory.Example;

public class ChatApplication
{
    private readonly IChatClient _chatClient;
    private readonly IObservationalMemoryStore _memoryStore;
    private readonly ObservationalMemoryContext _memoryContext;
    private readonly IObserverService _observerService;
    private readonly IReflectorService _reflectorService;
    private readonly ObservationalMemorySettings _settings;
    private readonly FileSystemObservationalMemoryStoreOptions _storageOptions;
    private readonly ILogger<ChatApplication> _logger;

    private readonly List<ChatMessage> _chatHistory = new();
    private const string UserId = "example-user";

    public ChatApplication(
        IChatClient chatClient,
        IObservationalMemoryStore memoryStore,
        ObservationalMemoryContext memoryContext,
        IObserverService observerService,
        IReflectorService reflectorService,
        IOptions<ObservationalMemorySettings> memorySettings,
        IOptions<FileSystemObservationalMemoryStoreOptions> storageOptions,
        ILogger<ChatApplication> logger)
    {
        _chatClient = chatClient;
        _memoryStore = memoryStore;
        _memoryContext = memoryContext;
        _observerService = observerService;
        _reflectorService = reflectorService;
        _settings = memorySettings.Value;
        _storageOptions = storageOptions.Value;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        AnsiConsole.Clear();
        ShowWelcomeScreen();

        var memory = await _memoryStore.LoadAsync(UserId);
        _memoryContext.Current = memory;

        while (true)
        {
            RenderUI(memory);

            var userInput = AnsiConsole.Prompt(
                new TextPrompt<string>("\n[cyan]You:[/] ")
                    .AllowEmpty());

            if (string.IsNullOrWhiteSpace(userInput))
                continue;

            if (userInput.Equals("/quit", StringComparison.OrdinalIgnoreCase) ||
                userInput.Equals("/exit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (userInput.Equals("/clear", StringComparison.OrdinalIgnoreCase))
            {
                _chatHistory.Clear();
                await _memoryStore.DeleteAsync(UserId);
                memory = new UserMemory();
                _memoryContext.Current = memory;
                AnsiConsole.Clear();
                AnsiConsole.MarkupLine("[yellow]Chat and memory cleared.[/]");
                continue;
            }

            if (userInput.Equals("/files", StringComparison.OrdinalIgnoreCase))
            {
                ShowMemoryFiles();
                continue;
            }

            // Add user message to history
            _chatHistory.Add(new ChatMessage(ChatRole.User, userInput));

            // Get AI response with memory context
            var assistantResponse = await GetAIResponseAsync();
            _chatHistory.Add(new ChatMessage(ChatRole.Assistant, assistantResponse));

            // Update observational memory
            memory = await UpdateMemoryAsync(memory, userInput, assistantResponse);

            AnsiConsole.Clear();
        }

        AnsiConsole.MarkupLine("\n[green]Goodbye! Your memory has been saved.[/]");
    }

    private void ShowWelcomeScreen()
    {
        var panel = new Panel(
            new Markup(
                "[bold yellow]AI Observational Memory Example[/]\n\n" +
                "This demo shows how the observational memory library works:\n" +
                $"• After [cyan]{_settings.ObserverRawMessageThreshold}[/] messages, raw exchanges are compressed into observations\n" +
                $"• After [cyan]{_settings.ReflectorObservationThreshold}[/] observations, they are deduplicated\n\n" +
                "[dim]Commands: /quit, /clear, /files[/]"))
        {
            Border = BoxBorder.Double,
            Padding = new Padding(2, 1),
            Width = 120 + 2
        };

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    private void RenderUI(UserMemory memory)
    {
        var grid = new Grid()
            .AddColumn(new GridColumn().Width(60))
            .AddColumn(new GridColumn().Width(60));

        // Left panel: Chat History
        var chatHistoryText = new StringBuilder();
        foreach (var msg in _chatHistory.TakeLast(5))
        {
            var role = msg.Role == ChatRole.User ? "[cyan]You[/]" : "[green]AI[/]";
            chatHistoryText.AppendLine($"{role}: {msg.Text}");
            chatHistoryText.AppendLine();
        }

        var chatPanel = new Panel(
            new Markup(chatHistoryText.Length > 0 
                ? chatHistoryText.ToString() 
                : "[dim]No messages yet. Start chatting![/]"))
        {
            Header = new PanelHeader("[bold]Chat History[/]"),
            Border = BoxBorder.Rounded,
            Expand = true
        };

        // Right panel: Memory Status
        var memoryStatus = new StringBuilder();
        memoryStatus.AppendLine($"[yellow]Raw Messages:[/] {memory.RawMessages.Count}/{_settings.ObserverRawMessageThreshold}");
        
        if (memory.RawMessages.Count > 0)
        {
            var progress = (double)memory.RawMessages.Count / _settings.ObserverRawMessageThreshold;
            memoryStatus.AppendLine(CreateProgressBar(progress));
        }
        
        memoryStatus.AppendLine();        
        if (memory.Observations.Count > 0)
        {
            const int ShowObservationsCount = 20;
            if (memory.Observations.Count > ShowObservationsCount)
            {
                memoryStatus.AppendLine($"[dim]Recent observations (last {ShowObservationsCount} of {memory.Observations.Count}):[/]");
            }
            else {
                memoryStatus.AppendLine($"[dim]Observations ({memory.Observations.Count}):[/]");
            }
            foreach (var obs in memory.Observations.TakeLast(ShowObservationsCount))
            {
                memoryStatus.AppendLine($"{obs.Importance} [dim]{obs.Timestamp:HH:mm}[/] {obs.Text}");
            }
        }
        else 
        {
            memoryStatus.AppendLine($"[yellow]No observations yet. Start chatting to create observations![/]");
        }

        var memoryPanel = new Panel(new Markup(memoryStatus.ToString()))
        {
            Header = new PanelHeader("[bold]Memory Status[/]"),
            Border = BoxBorder.Rounded,
            Expand = true
        };
        var memoryPanelAligned = Align.Center(memoryPanel, VerticalAlignment.Bottom);

        grid.AddRow(chatPanel, memoryPanelAligned);
        
        AnsiConsole.Write(grid);
    }

    private async Task<string> GetAIResponseAsync()
    {
        var systemMessage = "You are a friendly AI assistant. ";
        
        // Add memory context to system message
        var currentMemory = _memoryContext.Current;
        if (currentMemory?.Observations.Count > 0)
        {
            systemMessage += "\n\nWhat you remember about the user:\n";
            foreach (var obs in currentMemory.Observations)
            {
                systemMessage += $"- {obs.Text}\n";
            }
        }

        var messages = new List<ChatMessage> { new(ChatRole.System, systemMessage) };
        messages.AddRange(_chatHistory);

        var response = await _chatClient.GetResponseAsync(messages);
        return response.Text ?? string.Empty;
    }

    private async Task<UserMemory> UpdateMemoryAsync(UserMemory memory, string userMessage, string assistantResponse)
    {
        memory ??= new UserMemory();

        // Store raw message
        memory.RawMessages.Add(new RawMessage
        {
            Timestamp = DateTimeOffset.UtcNow,
            UserMessage = userMessage,
            AssistantResponse = assistantResponse
        });

        await _memoryStore.SaveAsync(UserId, memory);

        // Trigger observer if threshold reached
        if (memory.RawMessages.Count >= _settings.ObserverRawMessageThreshold)
        {
            AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .Start("[yellow]Extracting observations from conversation...[/]", ctx =>
                {
                    memory = _observerService.ObserveAsync(memory).GetAwaiter().GetResult();
                    _memoryStore.SaveAsync(UserId, memory).GetAwaiter().GetResult();
                });

            AnsiConsole.MarkupLine($"[green]✓[/] Extracted {memory.Observations.Count} observations!");
            Thread.Sleep(1500);

            // Trigger reflector if threshold reached
            if (memory.Observations.Count >= _settings.ReflectorObservationThreshold)
            {
                var beforeCount = memory.Observations.Count;
                
                AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .Start("[yellow]Consolidating observations...[/]", ctx =>
                    {
                        memory = _reflectorService.ReflectAsync(memory).GetAwaiter().GetResult();
                        _memoryStore.SaveAsync(UserId, memory).GetAwaiter().GetResult();
                    });

                AnsiConsole.MarkupLine($"[green]✓[/] Consolidated observations: {beforeCount} → {memory.Observations.Count}");
                Thread.Sleep(1500);
            }
        }

        _memoryContext.Current = memory;
        return memory;
    }

    private void ShowMemoryFiles()
    {
        var userDir = Path.Combine(_storageOptions.BaseDirectory, UserId);
        var observationsPath = Path.Combine(userDir, _storageOptions.ObservationsFileName);
        var rawMessagesPath = Path.Combine(userDir, _storageOptions.RawMessagesFileName);

        AnsiConsole.Clear();
        AnsiConsole.MarkupLine($"[bold]Memory File Locations:[/]\n");
        AnsiConsole.MarkupLine($"[cyan]Observations:[/] {observationsPath}");
        AnsiConsole.MarkupLine($"[cyan]Raw Messages:[/] {rawMessagesPath}");
        AnsiConsole.MarkupLine($"\n[dim]You can open these files in any text editor to see the memory content.[/]");
        AnsiConsole.MarkupLine("\n[yellow]Press any key to continue...[/]");
        Console.ReadKey(true);
        AnsiConsole.Clear();
    }

    private static string CreateProgressBar(double progress)
    {
        const int width = 20;
        var filled = (int)(progress * width);
        var bar = new string('█', filled) + new string('░', width - filled);
        return $"[cyan]{bar}[/] {progress:P0}";
    }
}
