using System.ComponentModel.DataAnnotations;

namespace AiObservationalMemory;

public class ObservationalMemorySettings
{
    /// <summary>
    /// Number of raw messages that triggers the observer to compress them into observations.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int ObserverRawMessageThreshold { get; init; } = 20;

    /// <summary>
    /// Number of observations that triggers the reflector to prune/deduplicate.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int ReflectorObservationThreshold { get; init; } = 50;
}
