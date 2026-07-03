using System.Text.Json.Serialization;

namespace DiagnosticServer.Mcp;

/// <summary>
/// Wraps a paginated collection of items with metadata for AI-friendly consumption.
/// </summary>
public class PaginatedResult<T>
{
    /// <summary>Items in the current page.</summary>
    [JsonPropertyName("items")]
    public List<T> Items { get; set; } = new();

    /// <summary>Current page number (1-based).</summary>
    [JsonPropertyName("page")]
    public int Page { get; set; } = 1;

    /// <summary>Number of items per page.</summary>
    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; } = 20;

    /// <summary>Total number of items across all pages.</summary>
    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    /// <summary>Whether more pages are available.</summary>
    [JsonPropertyName("hasMore")]
    public bool HasMore => Page * PageSize < TotalCount;

    /// <summary>AI-friendly insights about the data in this result.</summary>
    [JsonPropertyName("insights")]
    public QueryInsights? Insights { get; set; }
}

/// <summary>
/// AI-friendly insights produced from query results, including top consumers,
/// anomaly detection, and recommended follow-up actions.
/// </summary>
public class QueryInsights
{
    /// <summary>Human-readable summary of what the data shows.</summary>
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Top consumers by size/count. Each entry has a label, value, and optional percentage.
    /// </summary>
    [JsonPropertyName("topConsumers")]
    public List<InsightEntry> TopConsumers { get; set; } = new();

    /// <summary>Potential issues or anomalies detected in the data.</summary>
    [JsonPropertyName("anomalies")]
    public List<string> Anomalies { get; set; } = new();

    /// <summary>Recommended next actions for the AI to take.</summary>
    [JsonPropertyName("recommendedActions")]
    public List<string> RecommendedActions { get; set; } = new();
}

/// <summary>
/// A single insight entry representing a top consumer or notable data point.
/// </summary>
public class InsightEntry
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("percentage")]
    public double? Percentage { get; set; }

    /// <summary>Optional address or identifier for drill-down actions.</summary>
    [JsonPropertyName("address")]
    public string? Address { get; set; }
}

/// <summary>
/// Response from opening a dump, including session metadata and available tool catalog.
/// </summary>
public class OpenDumpResult
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("investigationKind")]
    public string InvestigationKind { get; set; } = "Dump";

    [JsonPropertyName("created")]
    public DateTime Created { get; set; }

    /// <summary>Target framework of the dump (e.g., ".NET 8.0").</summary>
    [JsonPropertyName("targetFramework")]
    public string? TargetFramework { get; set; }

    /// <summary>Number of heap objects in the dump.</summary>
    [JsonPropertyName("totalHeapObjects")]
    public long TotalHeapObjects { get; set; }

    /// <summary>Number of segments in the GC heap.</summary>
    [JsonPropertyName("segmentCount")]
    public int SegmentCount { get; set; }

    /// <summary>
    /// Catalog of diagnostic tools now available for this session.
    /// Returned to the AI so it knows what it can do with the opened dump.
    /// </summary>
    [JsonPropertyName("availableTools")]
    public List<ToolCatalogEntry> AvailableTools { get; set; } = new();
}

/// <summary>
/// Describes an available diagnostic tool for the AI's tool catalog.
/// </summary>
public class ToolCatalogEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = "query";

    [JsonPropertyName("parameters")]
    public List<string> Parameters { get; set; } = new();

    [JsonPropertyName("supportsFilter")]
    public bool SupportsFilter { get; set; }

    [JsonPropertyName("supportsPagination")]
    public bool SupportsPagination { get; set; } = true;
}
