namespace DiagnosticServer.Mcp;

/// <summary>
/// Configuration for the MCP server behavior — pagination defaults, limits, and transport settings.
/// Bound from the "Mcp" section in appsettings.json.
/// </summary>
public class McpConfiguration
{
    /// <summary>Default page size for paginated query results.</summary>
    public int DefaultPageSize { get; set; } = 10;

    /// <summary>Maximum allowed page size.</summary>
    public int MaxPageSize { get; set; } = 50;

    /// <summary>Default number of GC root paths to return.</summary>
    public int DefaultMaxPaths { get; set; } = 3;

    /// <summary>Maximum allowed GC root paths.</summary>
    public int MaxPaths { get; set; } = 10;

    /// <summary>Default number of top-N items in heap summaries.</summary>
    public int DefaultTopN { get; set; } = 20;

    /// <summary>Maximum number of insight entries (top consumers).</summary>
    public int MaxInsightEntries { get; set; } = 5;

    /// <summary>Endpoint path for the Streamable HTTP MCP transport.</summary>
    public string HttpEndpoint { get; set; } = "/mcp";
}
