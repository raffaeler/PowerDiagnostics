namespace DiagnosticModels;

/// <summary>
/// Complete GC root path analysis result for a target object.
/// </summary>
public class GcRootPathResult
{
    /// <summary>All root paths from GC roots to the target object.</summary>
    public List<GcRootPathNode> Paths { get; set; } = new();

    /// <summary>Total number of root paths found.</summary>
    public int TotalPaths { get; set; }

    /// <summary>Total number of object references across all paths.</summary>
    public int TotalReferences { get; set; }
}
