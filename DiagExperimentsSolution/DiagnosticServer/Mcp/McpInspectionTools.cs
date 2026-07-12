using System.Collections;
using System.ComponentModel;

using ClrDiagnostics;

using DiagnosticInvestigations;
using DiagnosticModels;

using Microsoft.Extensions.Options;

using ModelContextProtocol.Server;

using Microsoft.Diagnostics.Runtime;

namespace DiagnosticServer.Mcp;

/// <summary>
/// Tier 2 MCP inspection tools — dynamically registered when a dump is opened.
/// Provides object inspection, GC root path analysis, detail expansion, and memory map.
/// </summary>
[McpServerToolType]
public class McpInspectionTools
{
    private readonly McpSessionManager _sessionManager;
    private readonly QueriesService _queriesService;
    private readonly McpInsightsGenerator _insightsGenerator;
    private readonly McpConfiguration _config;
    private readonly ILogger<McpInspectionTools> _logger;

    public McpInspectionTools(
        McpSessionManager sessionManager,
        QueriesService queriesService,
        McpInsightsGenerator insightsGenerator,
        IOptions<McpConfiguration> config,
        ILogger<McpInspectionTools> logger)
    {
        _sessionManager = sessionManager;
        _queriesService = queriesService;
        _insightsGenerator = insightsGenerator;
        _config = config.Value;
        _logger = logger;
    }

    /// <summary>
    /// Get expanded detail rows for a specific query result item.
    /// For example, the individual ClrObject instances comprising a DumpHeapStat type group,
    /// or the stack frames within a thread.
    /// </summary>
    [McpServerTool(Name = "get_query_detail")]
    [Description("Get expanded detail rows for a specific query result. For example, the individual objects within a DumpHeapStat type group, or stack frames within a thread. Returns paginated detail rows. IMPORTANT: sourceName is NOT a tool name — it is the internal query identifier of the result set you want to drill into (see tool mappings in the description).")]
    public async Task<PaginatedResult<object>> GetQueryDetail(
        [Description("The session ID returned by open_dump.")] string sessionId,
        [Description("The internal diagnostic query identifier for the result set to expand. This is NOT a tool name. Tool-to-query mappings: query_heap_stat→'DumpHeapStat', query_static_fields→'GetStaticFieldsWithGraphAndSize', query_duplicate_strings→'GetDuplicateStrings', query_strings_by_size→'GetStringsBySize', query_modules→'Modules', query_thread_stacks→'Threads stacks', query_roots→'Roots', query_objects_by_size→'ObjectsBySize', query_non_system_objects→'NonSystemObjectsBySize', query_objects_by_allocator→'GetObjectsGroupedByAllocator (.NET5+ dumps)'.")] string sourceName,
        [Description("The 0-based row index of the master row to expand.")] int rowIndex,
        [Description("Page number (1-based) for detail pagination.")] int page = 1,
        [Description("Number of detail items per page (default 20, max 100).")] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, _config.MaxPageSize);

        // Resolve the query name, supporting both internal names and tool-suffix names
        var resolvedName = McpSessionManager.ToQueryName(sourceName) ?? sourceName;

        if (!_queriesService.Queries.TryGetValue(resolvedName, out var knownQuery))
            throw new InvalidOperationException($"Query '{resolvedName}' not found. Available: {string.Join(", ", _queriesService.Queries.Keys)}");

        if (!knownQuery.HasDetails || knownQuery.DetailProperty == null)
            throw new InvalidOperationException($"Query '{resolvedName}' has no expandable details.");

        var scope = McpToolRegistry.ValidateSession(_sessionManager, sessionId);
        var analyzer = scope.DiagnosticAnalyzer;

        _logger.LogInformation("MCP: get_query_detail on session {SessionId}, query={QueryName}, row={RowIndex}",
            sessionId, resolvedName, rowIndex);

        // Execute the query to get the master row
        var queryResult = knownQuery.ToQueryResult(analyzer, null);
        var rows = queryResult.Rows.Cast<object>().ToList();

        if (rowIndex < 0 || rowIndex >= rows.Count)
            throw new InvalidOperationException($"Row index {rowIndex} out of range (0-{rows.Count - 1}).");

        var masterRow = rows[rowIndex];

        // Get the detail property via reflection
        var detailProp = masterRow.GetType().GetProperty(knownQuery.DetailProperty);
        if (detailProp == null)
            throw new InvalidOperationException($"Detail property '{knownQuery.DetailProperty}' not found on row type.");

        var detailValue = detailProp.GetValue(masterRow);

        if (detailValue is IEnumerable detailEnumerable)
        {
            var detailRows = detailEnumerable.Cast<object>().ToList();
            var totalCount = detailRows.Count;
            var pagedDetail = detailRows
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(McpResultProjector.Project)
                .Where(o => o is not null)
                .Cast<object>()
                .ToList();

            return new PaginatedResult<object>
            {
                Items = pagedDetail,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
            };
        }

        // Single detail (e.g., static field's Obj property is a single ClrObject)
        var safeObj = McpResultProjector.Project(detailValue);
        return new PaginatedResult<object>
        {
            Items = safeObj is not null ? new List<object> { safeObj } : new List<object>(),
            Page = 1,
            PageSize = 1,
            TotalCount = 1,
        };
    }

    /// <summary>
    /// Get combined information for a heap address: field layout, containing object, and referencing objects.
    /// </summary>
    [McpServerTool(Name = "inspect_object")]
    [Description("Get combined information for a heap address: field layout (what fields it contains), data owner (what object contains this address), and referencing objects (what objects point to this). Use to understand the context and relationships of a heap object.")]
    public object InspectObject(
        [Description("The session ID returned by open_dump.")] string sessionId,
        [Description("Hex address of the object to inspect (e.g., '0x1a2b3c4d' or '1a2b3c4d').")] string address)
    {
        if (!TryParseHexAddress(address, out var addr))
            throw new ArgumentException($"Invalid hex address: '{address}'");

        var scope = McpToolRegistry.ValidateSession(_sessionManager, sessionId);
        var analyzer = scope.DiagnosticAnalyzer;

        _logger.LogInformation("MCP: inspect_object on session {SessionId}, address=0x{Address:X}",
            sessionId, addr);

        // Get field layout
        var layout = analyzer.GetObjectFieldLayout(addr);

        // Get data owner (containing object)
        var containing = analyzer.FindContainingObject(addr);
        object? dataOwnerInfo = null;
        if (containing is { } c)
        {
            bool isObjectStart = c.ContainingObject.Address == addr;
            var refs = isObjectStart
                ? analyzer.GetReferencingObjects(addr)
                : null;

            dataOwnerInfo = new
            {
                containingObjectAddress = $"0x{c.ContainingObject.Address:X}",
                containingObjectType = c.ContainingObject.Type?.Name ?? "Unknown",
                offsetWithinObject = c.OffsetWithinObject,
                objectSize = (long)c.ContainingObject.Size,
                isObjectStart,
                referencingObjects = refs?.Select(r => new
                {
                    address = $"0x{r.Address:X}",
                    typeName = r.TypeName,
                    fieldName = r.FieldName,
                }).ToList(),
            };
        }

        return new
        {
            address = $"0x{addr:X}",
            layout = layout != null ? new
            {
                typeName = layout.TypeName,
                mt = $"0x{layout.Mt:X}",
                size = layout.TotalSize,
                fields = layout.Fields?.Select(f => new
                {
                    name = f.FieldName,
                    typeName = f.TypeName,
                    offset = f.Offset,
                    value = f.ValueHex,
                    isReference = f.IsObjectReference,
                }).ToList(),
            } : null,
            dataOwner = dataOwnerInfo ?? new { kind = "Unmapped" },
        };
    }

    /// <summary>
    /// Find GC root paths keeping an address alive. Reveals why an object is not being collected.
    /// </summary>
    [McpServerTool(Name = "get_gc_roots")]
    [Description("Find GC root paths keeping an object alive. Reveals why an object is not being garbage collected. Each path shows the chain from a GC root through intermediate references to the target object.")]
    public async Task<object> GetGcRoots(
        [Description("The session ID returned by open_dump.")] string sessionId,
        [Description("Hex address of the object to find roots for.")] string address,
        [Description("Maximum number of root paths to return (default 3, max 10).")] int maxPaths = 3)
    {
        if (!TryParseHexAddress(address, out var addr))
            throw new ArgumentException($"Invalid hex address: '{address}'");

        maxPaths = Math.Clamp(maxPaths, 1, _config.MaxPaths);

        var scope = McpToolRegistry.ValidateSession(_sessionManager, sessionId);
        var analyzer = scope.DiagnosticAnalyzer;

        // Find the ClrObject at the given address
        ClrObject? clrObj = null;
        foreach (var obj in analyzer.Objects)
        {
            if (obj.Address == addr)
            {
                clrObj = obj;
                break;
            }
        }

        if (clrObj is not { } resolved)
            throw new InvalidOperationException($"No object found at address 0x{addr:X} in session '{sessionId}'.");

        _logger.LogInformation("MCP: get_gc_roots on session {SessionId}, address=0x{Address:X}, maxPaths={MaxPaths}",
            sessionId, addr, maxPaths);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await analyzer.GetRootPathsAsync(resolved, _ => { }, cts.Token, maxPaths);

        return new
        {
            address = $"0x{addr:X}",
            paths = result.Paths?.Select(p => new
            {
                rootKind = p.RootKind,
                rootAddress = p.ObjectAddress,
                typeName = p.TypeName,
                nodes = p.Children?.Select(n => new
                {
                    address = n.ObjectAddress,
                    typeName = n.TypeName,
                }).ToList(),
            }).ToList(),
            totalPaths = result.TotalPaths,
            totalReferences = result.TotalReferences,
        };
    }

    /// <summary>
    /// Get the objects directly referenced by the given object (1 level forward walk).
    /// Unlike get_gc_roots (which walks upward to roots), this walks forward through
    /// the object's instance fields to find what objects it references.
    /// Loads one level at a time — call again with a child address to drill deeper.
    /// </summary>
    [McpServerTool(Name = "get_referenced_objects")]
    [Description("Get objects directly referenced by a heap object (1 level forward walk through instance fields). Shows what objects the given address points to. Call again with a child address to drill deeper. Returns the target with its direct children.")]
    public object GetReferencedObjects(
        [Description("The session ID returned by open_dump.")] string sessionId,
        [Description("Hex address of the object to find forward references for (e.g., '0x1a2b3c4d').")] string address)
    {
        if (!TryParseHexAddress(address, out var addr))
            throw new ArgumentException($"Invalid hex address: '{address}'");

        var scope = McpToolRegistry.ValidateSession(_sessionManager, sessionId);
        var analyzer = scope.DiagnosticAnalyzer;

        // Find the ClrObject at the given address
        ClrObject? clrObj = null;
        foreach (var obj in analyzer.Objects)
        {
            if (obj.Address == addr)
            {
                clrObj = obj;
                break;
            }
        }

        if (clrObj is not { } resolved)
            throw new InvalidOperationException($"No object found at address 0x{addr:X} in session '{sessionId}'.");

        _logger.LogInformation("MCP: get_referenced_objects on session {SessionId}, address=0x{Address:X}",
            sessionId, addr);

        var hlp = new ClrDiagnostics.Experimental.DiagnosticAnalyzerHelper(analyzer);
        var result = hlp.GetForwardReferences(resolved.Address);

        return new
        {
            targetAddress = $"0x{addr:X}",
            targetTypeName = result.Paths?.FirstOrDefault()?.TypeName ?? "?",
            references = result.Paths?.FirstOrDefault()?.Children?.Select(c => new
            {
                address = c.ObjectAddress,
                typeName = c.TypeName,
                fieldName = c.ReferencingObjects?.FirstOrDefault()?.FieldName ?? "?",
            }).ToList(),
            totalReferences = result.TotalReferences,
        };
    }

    /// <summary>
    /// List all objects that hold references (instance or static fields) to a target heap object.
    /// Use to understand which objects point to a given object — essential for walking
    /// the object graph and understanding memory retention patterns.
    /// </summary>
    [McpServerTool(Name = "get_referencing_objects")]
    [Description("List all objects that hold references (instance or static fields) to a target heap object. Use to understand which objects point to a given object — essential for walking the object graph and understanding memory retention patterns. Returns paginated results.")]
    public object GetReferencingObjects(
        [Description("The session ID returned by open_dump.")] string sessionId,
        [Description("Hex address of the target object (e.g., '0x1a2b3c4d' or '1a2b3c4d').")] string address,
        [Description("Optional type name filter for referencing objects (case-insensitive substring match).")] string? typeNameFilter = null,
        [Description("Page number (1-based, default 1).")] int page = 1,
        [Description("Number of items per page (default 20, max 50).")] int pageSize = 20)
    {
        if (!TryParseHexAddress(address, out var addr))
            throw new ArgumentException($"Invalid hex address: '{address}'");

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, _config.MaxPageSize);

        var scope = McpToolRegistry.ValidateSession(_sessionManager, sessionId);
        var analyzer = scope.DiagnosticAnalyzer;

        // Validate the address is a valid object start
        ClrObject? clrObj = null;
        foreach (var obj in analyzer.Objects)
        {
            if (obj.Address == addr)
            {
                clrObj = obj;
                break;
            }
        }

        if (clrObj is not { } resolved)
            throw new InvalidOperationException($"No object found at address 0x{addr:X} in session '{sessionId}'.");

        _logger.LogInformation(
            "MCP: get_referencing_objects on session {SessionId}, address=0x{Address:X}, filter={Filter}",
            sessionId, addr, typeNameFilter ?? "(none)");

        var allRefs = analyzer.GetReferencingObjects(addr);

        // Apply type name filter if provided
        if (!string.IsNullOrWhiteSpace(typeNameFilter))
        {
            allRefs = allRefs
                .Where(r => r.TypeName.Contains(typeNameFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var totalCount = allRefs.Count;
        var pagedRefs = allRefs
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                address = $"0x{r.Address:X}",
                typeName = r.TypeName,
                fieldName = r.FieldName,
                isStatic = r.IsStatic,
            })
            .ToList();

        return new PaginatedResult<object>
        {
            Items = pagedRefs.Cast<object>().ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    /// <summary>
    /// Get the GC heap segment layout — shows per-generation heap distribution.
    /// </summary>
    [McpServerTool(Name = "get_memory_map")]
    [Description("Get the GC heap segment layout overview showing per-generation heap distribution, segment sizes, and address ranges. Useful for understanding the overall heap structure.")]
    public object GetMemoryMap(
        [Description("The session ID returned by open_dump.")] string sessionId)
    {
        var scope = McpToolRegistry.ValidateSession(_sessionManager, sessionId);
        var analyzer = scope.DiagnosticAnalyzer;

        _logger.LogInformation("MCP: get_memory_map on session {SessionId}", sessionId);

        var segments = analyzer.Heap.Segments.Select(s => new
        {
            start = $"0x{s.Start:X}",
            end = $"0x{s.End:X}",
            length = (long)(s.End - s.Start),
            generation = s.Kind switch
            {
                GCSegmentKind.Large => "LOH",
                GCSegmentKind.Pinned => "POH",
                GCSegmentKind.Frozen => "FOH",
                GCSegmentKind.Ephemeral => "Ephemeral",
                GCSegmentKind.Generation0 => "Gen0",
                GCSegmentKind.Generation1 => "Gen1",
                GCSegmentKind.Generation2 => "Gen2",
                _ => s.Kind.ToString(),
            },
            kind = s.Kind.ToString(),
        }).ToList();

        return new
        {
            totalSegments = segments.Count,
            totalHeapSize = segments.Sum(s => s.length),
            generationSummary = segments
                .GroupBy(s => s.generation)
                .Select(g => new
                {
                    generation = g.Key,
                    segmentCount = g.Count(),
                    totalSize = g.Sum(s => s.length),
                })
                .ToList(),
            segments,
        };
    }

    private static bool TryParseHexAddress(string hex, out ulong value)
    {
        hex = hex.Trim();
        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            hex = hex[2..];
        return ulong.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out value);
    }
}
