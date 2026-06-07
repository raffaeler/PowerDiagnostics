namespace DiagnosticModels;

/// <summary>
/// Represents a GC heap segment (memory region) in a loaded dump or snapshot.
/// </summary>
public class MemorySegmentInfo
{
    /// <summary>Start address of the segment (e.g., "0x000001A2B3C4D5E6").</summary>
    public string StartAddress { get; set; } = string.Empty;

    /// <summary>End address of the segment (exclusive).</summary>
    public string EndAddress { get; set; } = string.Empty;

    /// <summary>Start address of committed memory within the segment.</summary>
    public string CommittedStart { get; set; } = string.Empty;

    /// <summary>End address of committed memory within the segment (exclusive).</summary>
    public string CommittedEnd { get; set; } = string.Empty;

    /// <summary>Start address of reserved memory within the segment.</summary>
    public string ReservedStart { get; set; } = string.Empty;

    /// <summary>End address of reserved memory within the segment (exclusive).</summary>
    public string ReservedEnd { get; set; } = string.Empty;

    /// <summary>Segment kind: "Ephemeral", "Large", "Pinned", or "GCHeap".</summary>
    public string SegmentKind { get; set; } = string.Empty;

    /// <summary>True if this is the large object heap segment.</summary>
    public bool IsLargeObject { get; set; }

    /// <summary>True if this is the pinned object heap segment.</summary>
    public bool IsPinnedObject { get; set; }

    /// <summary>Number of heap objects in this segment.</summary>
    public long ObjectCount { get; set; }

    /// <summary>Total size of the segment in bytes.</summary>
    public long Size { get; set; }
}
