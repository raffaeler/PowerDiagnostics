using System.Collections;

namespace DiagnosticModels;

/// <summary>
/// Wraps query execution results with metadata about whether detail grids are available.
/// </summary>
public class QueryResult
{
    /// <summary>Name of the executed query (e.g., "DumpHeapStat").</summary>
    public string QueryName { get; set; } = string.Empty;

    /// <summary>Full type name of the result rows.</summary>
    public string ResultType { get; set; } = string.Empty;

    /// <summary>The query result rows.</summary>
    public IEnumerable Rows { get; set; } = Array.Empty<object>();

    /// <summary>True if this query supports a detail grid (master-detail pattern).</summary>
    public bool HasDetails { get; set; }

    /// <summary>Full type name of detail rows, if HasDetails is true.</summary>
    public string? DetailType { get; set; }

    /// <summary>
    /// Property name on each master row that yields the detail rows (e.g., "Objects", "StackFrames").
    /// Null if HasDetails is false.
    /// </summary>
    public string? DetailProperty { get; set; }

    /// <summary>
    /// Error message if the query execution failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
