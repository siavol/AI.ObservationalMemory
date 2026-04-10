# AI Observational Memory Example

This console application demonstrates the **AiObservationalMemory** library in action with a split-screen chat interface.

## Features

- **Live Chat UI**: Chat with an AI assistant that remembers you
- **Memory Visualization**: See raw messages and observations update in real-time
- **File System Storage**: Memory saved to human-readable markdown files
- **Progress Tracking**: Visual progress bar showing when observations will be extracted
- **Observer Demo**: Automatically compresses conversations after 5 messages
- **Reflector Demo**: Consolidates observations after 20 are collected

## Setup

### 1. Configure OpenAI API Key

```bash
cd src/AiObservationalMemory.Example
dotnet user-secrets set "OpenAI:ApiKey" "your-openai-api-key-here"
```

### 2. Build and Run

```bash
dotnet build
dotnet run
```

## How It Works

### Memory Flow

1. **Chat**: You send messages to the AI assistant
2. **Raw Storage**: Each exchange is stored as a raw message
3. **Observer Trigger**: After 5 messages, the Observer service compresses them into durable observations
4. **Reflector Trigger**: After 20 observations, the Reflector service deduplicates and consolidates them

### UI Layout

```
┌─────────────────────────────────┬──────────────────────────────┐
│ Chat History                    │ Memory Status                │
│                                 │                              │
│ You: Hello, my name is Alex     │ Raw Messages: 3/5            │
│ AI: Nice to meet you, Alex!     │ ███████████░░░░░░░░░ 60%    │
│                                 │                              │
│ You: I'm a vegetarian           │ Observations: 2              │
│ AI: I'll remember that!         │                              │
│                                 │ Recent observations:         │
│                                 │ 🔴 User's name is Alex       │
│                                 │ 🔴 User is vegetarian        │
└─────────────────────────────────┴──────────────────────────────┘

You: _
```

## Commands

- **Chat normally**: Just type your message and press Enter
- **/quit** or **/exit**: Exit the application
- **/clear**: Clear chat history and memory
- **/files**: Show the location of memory files on disk

## Memory Files

After chatting, you can view the memory files:

**Windows:**
```
%LOCALAPPDATA%\AiObservationalMemory.Example\example-user\
  memory.observations.md
  memory.raw.md
```

**Linux/Mac:**
```
~/.local/share/AiObservationalMemory.Example/example-user/
  memory.observations.md
  memory.raw.md
```

Use **/files** command in the app to see the exact path.

## Example Session

Try sharing personal information with the AI to see how observations are extracted:

- Tell it your name, occupation, location
- Share your preferences (food, hobbies, etc.)
- After 5 messages, watch the observer extract observations
- Ask "What do you know about me?" to see memory in action

## Customization

Edit Program.cs to change:
- Observer threshold (default: 5 messages)
- Reflector threshold (default: 20 observations)
- Storage location
- AI model (default: `gpt-4o-mini`)
