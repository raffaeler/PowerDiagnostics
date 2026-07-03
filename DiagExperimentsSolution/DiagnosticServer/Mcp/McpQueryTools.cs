using System.Collections;
using System.ComponentModel;
using System.Text.Json;

using ClrDiagnostics;

using DiagnosticInvestigations;
using DiagnosticModels;
using DiagnosticModels.Converters;

using Microsoft.Extensions.Options;

using ModelContextProtocol.Server;

using Microsoft.Diagnostics.Runtime;

namespace DiagnosticServer.Mcp;

/// <summary>
/// Tier 2 MCP tools — dynamically registered when a dump is opened.
/// Each query has its own dedicated tool with a clear description so the AI
/// knows exactly when to use it.
/// All query tools return paginated results with embedded insights.
/// </summary>
[McpServerToolType]
public class McpQueryTools
{
    private readonly McpSessionManager _sessionManager;
    private readonly QueriesService _queriesService;
    private readonly McpInsightsGenerator _insightsGenerator;
    private readonly McpConfiguration _config;
    private readonly ILogger<McpQueryTools> _logger;

    public McpQueryTools(
        McpSessionManager sessionManager,
        QueriesService queriesService,
        McpInsightsGenerator insightsGenerator,
        IOptions<McpConfiguration> config,
        ILogger<McpQueryTools> logger)
    {
        _sessionManager = sessionManager;
        _queriesService = queriesService;
        _insightsGenerator = insightsGenerator;
        _config = config.Value;
        _logger = logger;
    }

    // ──────────── Query tools ────────────

    /// <summary>
    /// Type-level heap statistics: object count and total graph size per type.
    /// This is the recommended first query on any new dump to understand memory composition.
    /// Requires an active dump session (use `open_dump` first).
    /// </summary>
    [McpServerTool(Name = "query_heap_stat")]
    [Description("Get type-level heap statistics: object count and total graph size per type. Start here on any new dump to understand memory composition. Returns top types by graph size with pagination. Requires an active dump session — use open_dump first if you haven't already.")]
    public async Task<PaginatedResult<object>> QueryHeapStat(
        [Description("The session ID returned by open_dump.")] string sessionId,
        [Description("Optional type name filter (case-insensitive substring match).")] string? filter = null,
        [Description("Page number (1-based).")] int page = 1,
        [Description("Number of items per page (default 20, max 100).")] int pageSize = 20)
    {
        return await ExecuteQueryAsync(sessionId, "DumpHeapStat", filter, page, pageSize);
    }

    /// <summary>
    /// Static field memory usage analysis. Use when suspecting static-rooted leaks or type initializer bloat.
    /// </summary>
    [McpServerTool(Name = "query_static_fields")]
    [Description("Analyze static field memory usage. Use when suspecting static-rooted leaks or type initializer bloat. Returns fields sorted by graph size with pagination.")]
    public async Task<PaginatedResult<object>> QueryStaticFields(
        [Description("The session ID returned by open_dump.")] string sessionId,
        [Description("Optional type name filter.")] string? filter = null,
        int page = 1, int pageSize = 20)
    {
        return await ExecuteQueryAsync(sessionId, "GetStaticFieldsWithGraphAndSize", filter, page, pageSize);
    }

    /// <summary>
    /// Find duplicate string instances wasting memory.
    /// </summary>
    [McpServerTool(Name = "query_duplicate_strings")]
    [Description("Find duplicate string instances in the heap. Use to detect string allocation waste — repeated identical strings each consuming separate memory.")]
    public async Task<PaginatedResult<object>> QueryDuplicateStrings(
        [Description("The session ID returned by open_dump.")] string sessionId,
        [Description("Optional text filter.")] string? filter = null,
        int page = 1, int pageSize = 20)
    {
        return await ExecuteQueryAsync(sessionId, "GetDuplicateStrings", filter, page, pageSize);
    }

    /// <summary>
    /// Largest string objects on the heap, sorted by size.
    /// </summary>
    [McpServerTool(Name = "query_strings_by_size")]
    [Description("List all heap strings sorted by size descending. Use to find large string objects consuming significant memory.")]
    public async Task<PaginatedResult<object>> QueryStringsBySize(
        [Description("The session ID returned by open_dump.")] string sessionId,
        [Description("Optional text filter.")] string? filter = null,
        int page = 1, int pageSize = 20)
    {
        return await ExecuteQueryAsync(sessionId, "GetStringsBySize", filter, page, pageSize);
    }

    /// <summary>
    /// List all loaded assemblies/modules.
    /// </summary>
    [McpServerTool(Name = "query_modules")]
    [Description("List all loaded assemblies/modules in the dump. Use to understand application module footprint or detect unexpected/unfamiliar assembly loads.")]
    public async Task<PaginatedResult<object>> QueryModules(
        [Description("The session ID returned by open_dump.")] string sessionId,
        [Description("Optional module name filter.")] string? filter = null,
        int page = 1, int pageSize = 20)
    {
        return await ExecuteQueryAsync(sessionId, "Modules", filter, page, pageSize);
    }

    /// <summary>
    /// Get managed thread call stacks. Use to understand what code is executing or diagnose deadlocks.
    /// </summary>
    [McpServerTool(Name = "query_thread_stacks")]
    [Description("Get managed thread call stacks. Use to understand what code is executing, diagnose deadlocks or stuck threads. Each thread has expandable stack frames via get_query_detail.")]
    public async Task<PaginatedResult<object>> QueryThreadStacks(
        [Description("The session ID returned by open_dump.")] string sessionId,
        [Description("Optional thread address filter.")] string? filter = null,
        int page = 1, int pageSize = 20)
    {
        return await ExecuteQueryAsync(sessionId, "Threads stacks", filter, page, pageSize);
    }

    /// <summary>
    /// List all GC roots (static, local, finalizer, etc.).
    /// </summary>
    [McpServerTool(Name = "query_roots")]
    [Description("List all GC roots (static, local, finalizer, etc.). Use to understand the root set that keeps objects alive in memory.")]
    public async Task<PaginatedResult<object>> QueryRoots(
        [Description("The session ID returned by open_dump.")] string sessionId,
        [Description("Optional root type name filter.")] string? filter = null,
        int page = 1, int pageSize = 20)
    {
        return await ExecuteQueryAsync(sessionId, "Roots", filter, page, pageSize);
    }

    /// <summary>
    /// List all heap objects sorted by size descending. Use to find the largest individual objects.
    /// </summary>
    [McpServerTool(Name = "query_objects_by_size")]
    [Description("List all heap objects sorted by size descending. Use to find the largest individual objects on the heap. Each result includes the object address for further inspection.")]
    public async Task<PaginatedResult<object>> QueryObjectsBySize(
        [Description("The session ID returned by open_dump.")] string sessionId,
        [Description("Optional type name filter.")] string? filter = null,
        int page = 1, int pageSize = 20)
    {
        return await ExecuteQueryAsync(sessionId, "ObjectsBySize", filter, page, pageSize);
    }

    /// <summary>
    /// Largest heap objects excluding System.* types. Focuses on application code allocations.
    /// </summary>
    [McpServerTool(Name = "query_non_system_objects")]
    [Description("List largest heap objects excluding System.*, Microsoft.*, and Interop types. Focuses on your application's own object allocations for leak detection and memory optimization.")]
    public async Task<PaginatedResult<object>> QueryNonSystemObjects(
        [Description("The session ID returned by open_dump.")] string sessionId,
        [Description("Optional type name filter.")] string? filter = null,
        int page = 1, int pageSize = 20)
    {
        return await ExecuteQueryAsync(sessionId, "NonSystemObjectsBySize", filter, page, pageSize);
    }

    /// <summary>
    /// Group objects by their allocating method/code path. Requires .NET5+ dumps.
    /// </summary>
    [McpServerTool(Name = "query_objects_by_allocator")]
    [Description("Group objects by their allocating method/code path. Use to identify which code paths generate the most objects. Requires .NET5+ dumps with allocator information.")]
    public async Task<PaginatedResult<object>> QueryObjectsByAllocator(
        [Description("The session ID returned by open_dump.")] string sessionId,
        [Description("Optional allocator name filter.")] string? filter = null,
        int page = 1, int pageSize = 20)
    {
        return await ExecuteQueryAsync(sessionId, "GetObjectsGroupedByAllocator (.NET5+ dumps)", filter, page, pageSize);
    }

    // ──────────── Helpers ────────────

    private async Task<PaginatedResult<object>> ExecuteQueryAsync(
        string sessionId,
        string queryName,
        string? filter,
        int page,
        int pageSize)
    {
        // Clamp pagination
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, _config.MaxPageSize);

        if (!_queriesService.Queries.TryGetValue(queryName, out var knownQuery))
        {
            throw new InvalidOperationException($"Query '{queryName}' not found. Available queries: {string.Join(", ", _queriesService.Queries.Keys)}");
        }

        var scope = McpToolRegistry.ValidateSession(_sessionManager, sessionId);
        var analyzer = scope.DiagnosticAnalyzer;

        _logger.LogInformation("MCP query: {QueryName} on session {SessionId} (page={Page}, pageSize={PageSize})",
            queryName, sessionId, page, pageSize);

        // Execute the query
        var queryResult = knownQuery.ToQueryResult(analyzer, filter);

        // Convert rows to list for pagination
        var rows = queryResult.Rows.Cast<object>().ToList();
        var totalCount = rows.Count;
        var pagedRows = rows
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(McpResultProjector.Project)
            .Where(o => o is not null)
            .Cast<object>()
            .ToList();

        // Generate insights from all rows (not just current page)
        var insights = _insightsGenerator.GenerateInsights(queryName, rows, filter);

        return new PaginatedResult<object>
        {
            Items = pagedRows,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Insights = insights,
        };
    }
}
