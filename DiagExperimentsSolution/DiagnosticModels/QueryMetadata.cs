namespace DiagnosticModels;

/// <summary>
/// Metadata about a diagnostic query, including column definitions for the master grid
/// and whether detail grids are supported.
/// </summary>
public class QueryMetadata
{
    /// <summary>Name of the query (e.g., "DumpHeapStat").</summary>
    public string QueryName { get; set; } = string.Empty;

    /// <summary>Full type name of the result rows.</summary>
    public string ResultType { get; set; } = string.Empty;

    /// <summary>True if this query supports a detail grid.</summary>
    public bool HasDetails { get; set; }

    /// <summary>Full type name of detail rows, if HasDetails is true.</summary>
    public string? DetailType { get; set; }

    /// <summary>
    /// Property name on each master row that yields the detail rows (e.g., "Objects", "StackFrames").
    /// Null if HasDetails is false.
    /// </summary>
    public string? DetailProperty { get; set; }

    /// <summary>Column definitions for the master DataGrid.</summary>
    public List<ColumnDefinition> Columns { get; set; } = new();

    /// <summary>Column definitions for the detail DataGrid, if HasDetails is true.</summary>
    public List<ColumnDefinition> DetailColumns { get; set; } = new();
}
