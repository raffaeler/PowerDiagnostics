namespace DiagnosticModels;

/// <summary>
/// A classified sub-range within a raw memory read, describing what object
/// (if any) owns this byte range.
/// </summary>
public class MemoryRegion
{
    /// <summary>Byte offset from the start of the raw memory read.</summary>
    public int Offset { get; set; }

    /// <summary>Length of this region in bytes.</summary>
    public int Length { get; set; }

    /// <summary>
    /// Kind of memory: "ObjectHeader", "InstanceField", "ArrayData",
    /// "StringData", "FreeBlock", or "Unmapped".
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Hex address of the containing object (if any).</summary>
    public string? ObjectAddress { get; set; }

    /// <summary>Type name of the containing object (if any).</summary>
    public string? ObjectTypeName { get; set; }

    /// <summary>Total size of the containing object in bytes.</summary>
    public long? ObjectSize { get; set; }

    /// <summary>Byte offset of this region within the containing object.</summary>
    public long? OffsetWithinObject { get; set; }
}

/// <summary>
/// Raw byte content at an arbitrary address in the dump, with optional
/// region partitioning showing which object owns each byte range.
/// </summary>
public class RawMemoryResult
{
    /// <summary>Hex address of the start of this memory read (e.g., "0x000001A2B3C4D5E6").</summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>Number of bytes in this read.</summary>
    public int Length { get; set; }

    /// <summary>Raw bytes encoded as base64 string.</summary>
    public string BytesBase64 { get; set; } = string.Empty;

    /// <summary>Overall region kind: "HeapObject", "FreeBlock", or "Unmapped".</summary>
    public string RegionKind { get; set; } = string.Empty;

    /// <summary>
    /// Sub-ranges within this memory read, partitioned by object boundaries.
    /// Each region describes who owns that byte range.
    /// May be empty if no objects were resolved for the range.
    /// </summary>
    public List<MemoryRegion> Regions { get; set; } = new();

    // ── Convenience flat properties (first/largest region's owner) ──

    /// <summary>Hex address of the containing object (first region with an owner).</summary>
    public string? ContainingObjectAddress { get; set; }

    /// <summary>Type name of the containing object (first region with an owner).</summary>
    public string? ContainingObjectTypeName { get; set; }

    /// <summary>Byte offset within the containing object (first region).</summary>
    public long? OffsetWithinObject { get; set; }
}
