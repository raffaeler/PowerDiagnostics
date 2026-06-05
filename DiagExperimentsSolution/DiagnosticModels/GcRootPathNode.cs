namespace DiagnosticModels;

/// <summary>
/// A node in a GC root path. Represents one step from a root to the target object.
/// </summary>
public class GcRootPathNode
{
    /// <summary>Hex address of this object (e.g., "0x000001A2B3C4D5E6").</summary>
    public string ObjectAddress { get; set; } = string.Empty;

    /// <summary>Type name of this object.</summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>The kind of GC root (e.g., "StaticVar", "StackLocal", "Finalizer").</summary>
    public string RootKind { get; set; } = string.Empty;

    /// <summary>Depth from the root (0 = root itself).</summary>
    public int Depth { get; set; }

    /// <summary>Child nodes representing the chain from this node toward the target object.</summary>
    public List<GcRootPathNode> Children { get; set; } = new();

    /// <summary>Objects that hold references to this node.</summary>
    public List<GcReferenceInfo> ReferencingObjects { get; set; } = new();
}
