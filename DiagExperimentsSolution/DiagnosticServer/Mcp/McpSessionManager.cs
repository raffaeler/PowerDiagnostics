using ClrDiagnostics;

using DiagnosticInvestigations;
using DiagnosticInvestigations.Configurations;
using DiagnosticServer.Services;

using Microsoft.Extensions.Options;

namespace DiagnosticServer.Mcp;

/// <summary>
/// Provides session management helpers for MCP tools.
/// Wraps <see cref="DebuggingSessionService"/> and <see cref="InvestigationState"/>
/// to handle dump opening, session listing, and session closing with consistent error handling.
/// Also manages the lifecycle of dynamic (Tier 2) tool registration via <see cref="McpToolRegistry"/>.
/// </summary>
public class McpSessionManager
{
    private readonly DebuggingSessionService _debuggingSessionService;
    private readonly InvestigationState _investigationState;
    private readonly QueriesService _queriesService;
    private readonly GeneralConfiguration _generalConfig;
    private readonly ILogger<McpSessionManager> _logger;

    public McpSessionManager(
        DebuggingSessionService debuggingSessionService,
        InvestigationState investigationState,
        QueriesService queriesService,
        IOptions<GeneralConfiguration> generalConfig,
        ILogger<McpSessionManager> logger)
    {
        _debuggingSessionService = debuggingSessionService;
        _investigationState = investigationState;
        _queriesService = queriesService;
        _generalConfig = generalConfig.Value;
        _logger = logger;
    }

    /// <summary>
    /// Lists all .dmp and .mdmp files in the configured dumps folder.
    /// </summary>
    public List<string> ListDumps()
    {
        var folder = _generalConfig.DumpsFolder;
        if (!Directory.Exists(folder))
            return new List<string>();

        var dmpFiles = Directory.EnumerateFiles(folder, "*.dmp").Select(f => Path.GetFileName(f)!);
        var mdmpFiles = Directory.EnumerateFiles(folder, "*.mdmp").Select(f => Path.GetFileName(f)!);
        return dmpFiles.Concat(mdmpFiles).OrderBy(f => f).ToList();
    }

    /// <summary>
    /// Opens a dump file by name from the configured dumps folder.
    /// On success, dynamically registers Tier 2 diagnostic tools.
    /// </summary>
    public async Task<OpenDumpResult> OpenDumpAsync(string dumpName)
    {
        var dumpsFolder = Path.GetFullPath(_generalConfig.DumpsFolder);
        var resolved = Path.GetFullPath(Path.Combine(dumpsFolder, dumpName));

        // Path-traversal guard
        if (!resolved.StartsWith(dumpsFolder, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Dump file must be inside the configured dumps folder: {dumpsFolder}");

        if (!File.Exists(resolved))
            throw new FileNotFoundException($"Dump file not found: {dumpName}");

        var sessionId = await _debuggingSessionService.OpenDumpFromFile(resolved);

        // Try to get basic heap info for the catalog response
        var scope = _investigationState.GetInvestigationScope(sessionId);
        long totalObjects = 0;
        int segments = 0;
        string? targetFramework = null;

        if (scope != null)
        {
            try
            {
                totalObjects = scope.DiagnosticAnalyzer.Objects.Count();
                segments = scope.DiagnosticAnalyzer.Heap.Segments.Count();
                targetFramework = scope.DiagnosticAnalyzer.ClrRuntime.ClrInfo.Flavor.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not read heap metadata for session {SessionId}", sessionId);
            }
        }

        var result = new OpenDumpResult
        {
            SessionId = sessionId,
            InvestigationKind = "Dump",
            Created = DateTime.Now,
            TargetFramework = targetFramework,
            TotalHeapObjects = totalObjects,
            SegmentCount = segments,
            AvailableTools = GetToolCatalog(),
        };

        return result;
    }

    /// <summary>
    /// Returns the list of active diagnostic sessions.
    /// </summary>
    public IList<InvestigationScope> GetActiveSessions()
    {
        return _investigationState.GetActiveSessions();
    }

    /// <summary>
    /// Closes a session and releases resources.
    /// </summary>
    public async Task CloseSessionAsync(string sessionId)
    {
        await _debuggingSessionService.CloseSession(sessionId);
    }

    /// <summary>
    /// Gets the diagnostic analyzer for a session, or null if not found.
    /// </summary>
    public DiagnosticAnalyzer? GetAnalyzer(string sessionId)
    {
        var scope = _investigationState.GetInvestigationScope(sessionId);
        return scope?.DiagnosticAnalyzer;
    }

    /// <summary>
    /// Gets the full investigation scope for a session, or null if not found.
    /// </summary>
    public InvestigationScope? GetInvestigationScope(string sessionId)
    {
        return _investigationState.GetInvestigationScope(sessionId);
    }

    /// <summary>
    /// Gets the queries service for enumerating available queries.
    /// </summary>
    public QueriesService QueriesService => _queriesService;

    /// <summary>
    /// Builds the tool catalog describing all available Tier 2 diagnostic tools.
    /// Returned when a dump is opened so the AI knows what operations are available.
    /// </summary>
    private List<ToolCatalogEntry> GetToolCatalog()
    {
        var catalog = new List<ToolCatalogEntry>();

        // Query tools
        foreach (var queryName in _queriesService.Queries.Keys)
        {
            catalog.Add(new ToolCatalogEntry
            {
                Name = $"query_{ToToolName(queryName)}",
                Description = GetQueryDescription(queryName),
                Category = "query",
                Parameters = new List<string> { "sessionId", "filter?", "page?", "pageSize?" },
                SupportsFilter = true,
                SupportsPagination = true,
            });
        }

        // Inspection tools
        catalog.Add(new ToolCatalogEntry
        {
            Name = "get_query_detail",
            Description = "Get expanded detail rows for a specific query result item (e.g., objects in a heap stat group, stack frames in a thread). sourceName is NOT a tool name — it is the internal query identifier.",
            Category = "detail",
            Parameters = new List<string> { "sessionId", "sourceName", "rowIndex", "page?", "pageSize?" },
            SupportsPagination = true,
        });

        catalog.Add(new ToolCatalogEntry
        {
            Name = "inspect_object",
            Description = "Get combined information for a heap address: field layout, containing object (data owner), and referencing objects.",
            Category = "inspection",
            Parameters = new List<string> { "sessionId", "address" },
        });

        catalog.Add(new ToolCatalogEntry
        {
            Name = "get_referencing_objects",
            Description = "List all objects that hold references (instance or static fields) to a target heap object. Supports optional type name filtering and pagination.",
            Category = "inspection",
            Parameters = new List<string> { "sessionId", "address", "typeNameFilter?", "page?", "pageSize?" },
            SupportsFilter = true,
            SupportsPagination = true,
        });

        catalog.Add(new ToolCatalogEntry
        {
            Name = "get_gc_roots",
            Description = "Find GC root paths keeping an address alive — reveals why an object is not being garbage collected.",
            Category = "inspection",
            Parameters = new List<string> { "sessionId", "address", "maxPaths?" },
            SupportsPagination = true,
        });

        catalog.Add(new ToolCatalogEntry
        {
            Name = "get_memory_map",
            Description = "Get the GC heap segment layout overview — shows per-generation heap distribution.",
            Category = "inspection",
            Parameters = new List<string> { "sessionId" },
        });

        return catalog;
    }

    /// <summary>
    /// Converts a query name (e.g., "DumpHeapStat") to a tool name (e.g., "heap_stat").
    /// </summary>
    internal static string ToToolName(string queryName)
    {
        return queryName switch
        {
            "DumpHeapStat" => "heap_stat",
            "GetStaticFieldsWithGraphAndSize" => "static_fields",
            "GetDuplicateStrings" => "duplicate_strings",
            "GetStringsBySize" => "strings_by_size",
            "Modules" => "modules",
            "Threads stacks" => "thread_stacks",
            "Roots" => "roots",
            "ObjectsBySize" => "objects_by_size",
            "NonSystemObjectsBySize" => "non_system_objects",
            "GetObjectsGroupedByAllocator (.NET5+ dumps)" => "objects_by_allocator",
            _ => queryName.ToLowerInvariant().Replace(" ", "_").Replace("(", "").Replace(")", "").Replace("+", "_plus"),
        };
    }

    /// <summary>
    /// Reverse mapping from tool name suffix to query name.
    /// </summary>
    public static string? ToQueryName(string toolName)
    {
        return toolName switch
        {
            "heap_stat" => "DumpHeapStat",
            "static_fields" => "GetStaticFieldsWithGraphAndSize",
            "duplicate_strings" => "GetDuplicateStrings",
            "strings_by_size" => "GetStringsBySize",
            "modules" => "Modules",
            "thread_stacks" => "Threads stacks",
            "roots" => "Roots",
            "objects_by_size" => "ObjectsBySize",
            "non_system_objects" => "NonSystemObjectsBySize",
            "objects_by_allocator" => "GetObjectsGroupedByAllocator (.NET5+ dumps)",
            _ => null,
        };
    }

    /// <summary>
    /// Gets an AI-friendly description for a query.
    /// </summary>
    private static string GetQueryDescription(string queryName)
    {
        return queryName switch
        {
            "DumpHeapStat" => "Type-level heap statistics: count and total graph size per type. Start here on any new dump to understand memory composition.",
            "GetStaticFieldsWithGraphAndSize" => "Static field memory usage analysis. Use when suspecting static-rooted leaks or type initializer bloat.",
            "GetDuplicateStrings" => "Find duplicate string instances in the heap. Use to detect string allocation waste.",
            "GetStringsBySize" => "List all heap strings sorted by size descending. Use to find large string objects consuming memory.",
            "Modules" => "List all loaded assemblies/modules. Use to understand application module footprint or detect unexpected assemblies.",
            "Threads stacks" => "Get managed thread call stacks. Use to understand what code is executing or diagnose deadlocks.",
            "Roots" => "List all GC roots (static, local, finalizer, etc.). Use to understand the root set keeping objects alive.",
            "ObjectsBySize" => "List all heap objects sorted by size descending. Use to find the largest individual objects on the heap.",
            "NonSystemObjectsBySize" => "Largest heap objects excluding System.* types. Focus on your application's own object allocations.",
            "GetObjectsGroupedByAllocator (.NET5+ dumps)" => "Group objects by their allocating method/code path. Identify which code paths generate the most objects. Requires .NET5+ dumps.",
            _ => $"Execute the '{queryName}' diagnostic query.",
        };
    }
}
