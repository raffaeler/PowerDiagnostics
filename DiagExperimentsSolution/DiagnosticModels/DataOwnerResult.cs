namespace DiagnosticModels;

/// <summary>
/// Describes which object (if any) owns a given address in the dump.
/// Always applicable — works for interior addresses and data, not just object starts.
/// </summary>
public class DataOwnerResult
{
    /// <summary>Hex address being queried (e.g., "0x000001A2B3C4D5E6").</summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// Kind of address: "ObjectStart", "InsideObject", "FreeBlock", or "Unmapped".
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Hex address of the containing object (if Kind is ObjectStart or InsideObject).</summary>
    public string? ContainingObjectAddress { get; set; }

    /// <summary>Type name of the containing object.</summary>
    public string? ContainingObjectTypeName { get; set; }

    /// <summary>Byte offset within the containing object.</summary>
    public long? OffsetWithinObject { get; set; }

    /// <summary>Total size of the containing object in bytes.</summary>
    public long? ObjectSize { get; set; }

    /// <summary>True if this address is the exact start of a heap object.</summary>
    public bool IsObjectStart { get; set; }

    /// <summary>
    /// When IsObjectStart is true, lists all objects that hold references to this
    /// object. Null when the address is not an object start.
    /// </summary>
    public List<GcReferenceInfo>? ReferencingObjects { get; set; }
}

/// <summary>
/// Lists objects that hold references to a target object address.
/// Only meaningful for object-start addresses.
/// </summary>
public class ReferencingObjectsResult
{
    /// <summary>Hex address of the target object.</summary>
    public string TargetAddress { get; set; } = string.Empty;

    /// <summary>True if the target address is a valid object start.</summary>
    public bool IsObjectStart { get; set; }

    /// <summary>List of objects holding references to the target (empty if not an object start).</summary>
    public List<GcReferenceInfo> ReferencingObjects { get; set; } = new();
}

/// <summary>
/// Combined address information: data owner + optional field layout + optional
/// referencing objects. Convenience endpoint for the frontend address detail view.
/// </summary>
public class AddressInfoResult
{
    /// <summary>Hex address being queried.</summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>Kind: "ObjectStart", "InsideObject", "FreeBlock", or "Unmapped".</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Hex address of the containing object.</summary>
    public string? ContainingObjectAddress { get; set; }

    /// <summary>Type name of the containing object.</summary>
    public string? ContainingObjectTypeName { get; set; }

    /// <summary>Byte offset within the containing object.</summary>
    public long? OffsetWithinObject { get; set; }

    /// <summary>Total size of the containing object in bytes.</summary>
    public long? ObjectSize { get; set; }

    /// <summary>True if this address is the exact start of a heap object.</summary>
    public bool IsObjectStart { get; set; }

    /// <summary>Field layout (only when IsObjectStart is true).</summary>
    public ObjectFieldLayout? FieldLayout { get; set; }

    /// <summary>Referencing objects (only when IsObjectStart is true).</summary>
    public List<GcReferenceInfo>? ReferencingObjects { get; set; }
}
