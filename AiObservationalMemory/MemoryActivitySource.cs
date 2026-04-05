using System.Diagnostics;

namespace AiObservationalMemory;

/// <summary>
/// Shared ActivitySource for observational memory operations.
/// </summary>
public static class MemoryActivitySource
{
    public static readonly ActivitySource Source = new("AiObservationalMemory");

    public static class SpanNames
    {
        public const string Load = "Memory.Load";
        public const string Save = "Memory.Save";
        public const string Delete = "Memory.Delete";
        public const string Observe = "Memory.Observe";
        public const string Reflect = "Memory.Reflect";
    }

    public static class TagKeys
    {
        public const string UserId = "memory.user_id";
        public const string ObservationsCount = "memory.observations.count";
        public const string RawMessagesCount = "memory.raw_messages.count";
    }
}
