using System.ComponentModel.DataAnnotations;

namespace Svl.AI.ObservationalMemory;

/// <summary>
/// Configuration options for file system-based observational memory store.
/// </summary>
public sealed class FileSystemObservationalMemoryStoreOptions
{
    /// <summary>
    /// Base directory path where user memory files are stored.
    /// Each user gets a subdirectory: {BaseDirectory}/{userId}/
    /// </summary>
    [Required]
    public string BaseDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ObservationalMemory");

    /// <summary>
    /// File name for storing observations (plain text markdown).
    /// </summary>
    [Required]
    public string ObservationsFileName { get; set; } = "memory.observations.md";

    /// <summary>
    /// File name for storing raw message exchanges (plain text markdown).
    /// </summary>
    [Required]
    public string RawMessagesFileName { get; set; } = "memory.raw.md";
}
