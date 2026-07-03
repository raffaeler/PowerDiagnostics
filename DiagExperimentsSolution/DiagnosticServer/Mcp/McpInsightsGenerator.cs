using System.Globalization;

using DiagnosticModels;

using Microsoft.Diagnostics.Runtime;
using Microsoft.Extensions.Options;

namespace DiagnosticServer.Mcp;

/// <summary>
/// Generates AI-friendly insights from diagnostic query results.
/// Produces human-readable summaries, top-N consumer lists, anomaly detection,
/// and recommended follow-up actions tailored to each query type.
/// </summary>
public class McpInsightsGenerator
{
    private readonly McpConfiguration _config;
    private readonly ILogger<McpInsightsGenerator> _logger;

    public McpInsightsGenerator(IOptions<McpConfiguration> config, ILogger<McpInsightsGenerator> logger)
    {
        _config = config.Value;
        _logger = logger;
    }

    /// <summary>
    /// Generates insights based on the query type and its full result set.
    /// </summary>
    public QueryInsights? GenerateInsights(string queryName, List<object> allRows, string? filter)
    {
        if (allRows.Count == 0)
        {
            return new QueryInsights
            {
                Summary = filter != null
                    ? $"No results found for query '{queryName}' with filter '{filter}'. Try a broader filter or no filter."
                    : $"No results found for query '{queryName}'. The dump may be empty or the query produced no results.",
            };
        }

        return queryName switch
        {
            "DumpHeapStat" => GenerateHeapStatInsights(allRows, filter),
            "GetStaticFieldsWithGraphAndSize" => GenerateStaticFieldsInsights(allRows, filter),
            "GetDuplicateStrings" => GenerateDuplicateStringsInsights(allRows, filter),
            "GetStringsBySize" => GenerateStringsBySizeInsights(allRows, filter),
            "Modules" => GenerateModulesInsights(allRows, filter),
            "Threads stacks" => GenerateThreadStacksInsights(allRows, filter),
            "Roots" => GenerateRootsInsights(allRows, filter),
            "ObjectsBySize" or "NonSystemObjectsBySize" => GenerateObjectsBySizeInsights(allRows, queryName, filter),
            "GetObjectsGroupedByAllocator (.NET5+ dumps)" => GenerateAllocatorInsights(allRows, filter),
            _ => GenerateGenericInsights(queryName, allRows, filter),
        };
    }

    private QueryInsights GenerateHeapStatInsights(List<object> rows, string? filter)
    {
        var stats = rows.OfType<DbmDumpHeapStat>()
            .OrderByDescending(s => s.GraphSize)
            .ToList();

        var totalObjects = stats.Sum(s => (long)s.Objects.Count);
        var totalSize = stats.Sum(s => (long)s.GraphSize);
        var topN = stats.Take(_config.MaxInsightEntries).ToList();

        var insights = new QueryInsights
        {
            Summary = filter != null
                ? $"Heap stat filtered by '{filter}': {stats.Count} types, {totalObjects:N0} total objects, {FormatSize(totalSize)} total graph size."
                : $"Heap contains {stats.Count} distinct types, {totalObjects:N0} total objects, {FormatSize(totalSize)} total graph size.",
            TopConsumers = topN.Select(s => new InsightEntry
            {
                Label = s.TypeName ?? "(unknown)",
                Value = $"{s.Objects:N0} objects, {FormatSize((long)s.GraphSize)}",
                Percentage = totalSize > 0 ? Math.Round((double)s.GraphSize / totalSize * 100, 1) : 0,
                Address = s.MT > 0 ? $"0x{s.MT:X}" : null,
            }).ToList(),
        };

        // Anomaly detection
        foreach (var t in topN)
        {
            var pct = totalSize > 0 ? (double)t.GraphSize / totalSize * 100 : 0;
            if (pct > 30)
                insights.Anomalies.Add($"'{t.TypeName}' consumes {pct:F1}% of the heap — this is unusually high.");
        }

        if (totalSize > 1_000_000_000)
            insights.Anomalies.Add($"Total heap size ({FormatSize(totalSize)}) exceeds 1 GB — possible memory leak.");

        // Recommendations
        if (topN.Count > 0)
        {
            insights.RecommendedActions.Add($"Use `inspect_object` on objects from the top consumers to understand their content.");
            insights.RecommendedActions.Add($"Use `get_gc_roots` on large object addresses to understand why they are alive.");
        }

        return insights;
    }

    private QueryInsights GenerateStaticFieldsInsights(List<object> rows, string? filter)
    {
        var stats = rows.OfType<DbmStaticFields>()
            .OrderByDescending(s => s.Size)
            .ToList();

        var totalSize = stats.Sum(s => s.Size);
        var topN = stats.Take(_config.MaxInsightEntries).ToList();

        var insights = new QueryInsights
        {
            Summary = filter != null
                ? $"Static fields filtered by '{filter}': {stats.Count} entries, {FormatSize(totalSize)} total size."
                : $"Found {stats.Count} static field entries totaling {FormatSize(totalSize)}.",
            TopConsumers = topN.Select(s => new InsightEntry
            {
                Label = s.Field?.Name ?? "(unknown field)",
                Value = FormatSize(s.Size),
                Percentage = totalSize > 0 ? Math.Round((double)s.Size / totalSize * 100, 1) : 0,
            }).ToList(),
        };

        if (totalSize > 100_000_000)
            insights.Anomalies.Add($"Static field memory ({FormatSize(totalSize)}) is significant — possible static-rooted memory leak.");

        if (topN.Count > 0)
            insights.RecommendedActions.Add("Use `inspect_object` on statics with large graphs to trace referenced objects.");

        return insights;
    }

    private QueryInsights GenerateDuplicateStringsInsights(List<object> rows, string? filter)
    {
        var strings = rows.OfType<DbmDupStrings>()
            .OrderByDescending(s => s.Count)
            .ToList();

        var totalDuplicates = strings.Sum(s => s.Count);
        var topN = strings.Take(_config.MaxInsightEntries).ToList();

        var insights = new QueryInsights
        {
            Summary = filter != null
                ? $"Duplicate strings filtered by '{filter}': {strings.Count} unique texts, {totalDuplicates:N0} duplicate instances."
                : $"Found {strings.Count} duplicate string values with {totalDuplicates:N0} excess instances — potential for string interning.",
            TopConsumers = topN.Select(s => new InsightEntry
            {
                Label = Truncate(s.Text ?? "", 80),
                Value = $"{s.Count:N0} duplicates",
            }).ToList(),
        };

        if (totalDuplicates > 1000)
            insights.Anomalies.Add($"{totalDuplicates:N0} duplicate string instances detected — consider using string interning (string.Intern) for frequently repeated values.");

        insights.RecommendedActions.Add("Consider calling string.Intern() on frequently repeated string values to reduce memory usage.");

        return insights;
    }

    private QueryInsights GenerateStringsBySizeInsights(List<object> rows, string? filter)
    {
        var strings = rows.OfType<DbmStringsBySize>()
            .OrderByDescending(s => s.Obj.Size)
            .ToList();

        var totalSize = strings.Sum(s => (long)s.Obj.Size);
        var topN = strings.Take(_config.MaxInsightEntries).ToList();

        var insights = new QueryInsights
        {
            Summary = filter != null
                ? $"Strings by size filtered by '{filter}': {strings.Count} strings, {FormatSize(totalSize)} total."
                : $"Found {strings.Count} strings totaling {FormatSize(totalSize)}.",
            TopConsumers = topN.Select(s => new InsightEntry
            {
                Label = Truncate(s.Text ?? "", 80),
                Value = FormatSize((long)s.Obj.Size),
                Address = $"0x{s.Obj.Address:X}",
            }).ToList(),
        };

        if (topN.Count > 0)
            insights.RecommendedActions.Add("Use `inspect_object` on large string addresses to see referencing objects.");

        return insights;
    }

    private QueryInsights GenerateModulesInsights(List<object> rows, string? filter)
    {
        var modules = rows.OfType<ClrModule>().ToList();

        var systemModules = modules.Count(m => m.Name?.StartsWith("System") == true || m.Name?.StartsWith("Microsoft") == true);
        var appModules = modules.Count - systemModules;

        var insights = new QueryInsights
        {
            Summary = filter != null
                ? $"Modules filtered by '{filter}': {modules.Count} assemblies."
                : $"Loaded {modules.Count} assemblies: {appModules} application/third-party, {systemModules} system/Microsoft.",
            TopConsumers = modules.Take(_config.MaxInsightEntries).Select(m => new InsightEntry
            {
                Label = Path.GetFileName(m.Name ?? "(unknown)"),
                Value = m.Name ?? "",
            }).ToList(),
        };

        if (appModules > 200)
            insights.Anomalies.Add($"{appModules} non-system modules loaded — possible AssemblyLoadContext leak or excessive dependency loading.");

        return insights;
    }

    private QueryInsights GenerateThreadStacksInsights(List<object> rows, string? filter)
    {
        var stacks = rows.OfType<DbmStackFrame>().ToList();
        var totalFrames = stacks.Sum(s => s.StackFrames?.Count() ?? 0);

        var insights = new QueryInsights
        {
            Summary = filter != null
                ? $"Thread stacks filtered by '{filter}': {stacks.Count} threads, {totalFrames} total stack frames."
                : $"Found {stacks.Count} managed threads with {totalFrames} total stack frames.",
        };

        if (stacks.Count > 100)
            insights.Anomalies.Add($"{stacks.Count} managed threads — high thread count may indicate thread pool starvation or runaway thread creation.");

        return insights;
    }

    private QueryInsights GenerateRootsInsights(List<object> rows, string? filter)
    {
        var roots = rows.OfType<ClrRoot>().ToList();
        var rootKinds = roots.GroupBy(r => r.RootKind)
            .OrderByDescending(g => g.Count())
            .ToList();

        var insights = new QueryInsights
        {
            Summary = filter != null
                ? $"GC roots filtered by '{filter}': {roots.Count} total roots."
                : $"Found {roots.Count} total GC roots.",
            TopConsumers = rootKinds.Take(_config.MaxInsightEntries).Select(g => new InsightEntry
            {
                Label = g.Key.ToString(),
                Value = $"{g.Count():N0} roots",
            }).ToList(),
        };

        return insights;
    }

    private QueryInsights GenerateObjectsBySizeInsights(List<object> rows, string queryName, string? filter)
    {
        var objects = rows.OfType<ClrObject>()
            .OrderByDescending(o => o.Size)
            .ToList();

        var totalSize = objects.Sum(o => (long)o.Size);
        var topN = objects.Take(_config.MaxInsightEntries).ToList();

        var insights = new QueryInsights
        {
            Summary = filter != null
                ? $"Objects by size filtered by '{filter}': {objects.Count} objects, {FormatSize(totalSize)} total."
                : $"Found {objects.Count} objects totaling {FormatSize(totalSize)}. Top types: {string.Join(", ", objects.GroupBy(o => o.Type?.Name).OrderByDescending(g => g.Sum(o => (long)o.Size)).Take(3).Select(g => g.Key ?? "(unknown)"))}.",
            TopConsumers = topN.Select(o => new InsightEntry
            {
                Label = o.Type?.Name ?? "(unknown)",
                Value = FormatSize((long)o.Size),
                Address = $"0x{o.Address:X}",
            }).ToList(),
        };

        if (topN.Count > 0)
        {
            insights.RecommendedActions.Add("Use `inspect_object` on any address to see its fields and references.");
            insights.RecommendedActions.Add("Use `get_gc_roots` on large objects to understand why they remain alive.");
        }

        return insights;
    }

    private QueryInsights GenerateAllocatorInsights(List<object> rows, string? filter)
    {
        var groups = rows.OfType<DbmAllocatorGroup>()
            .OrderByDescending(g => g.Objects?.Count() ?? 0)
            .ToList();

        var totalObjects = groups.Sum(g => g.Objects?.Count() ?? 0);
        var topN = groups.Take(_config.MaxInsightEntries).ToList();

        var insights = new QueryInsights
        {
            Summary = filter != null
                ? $"Allocator groups filtered by '{filter}': {groups.Count} groups, {totalObjects:N0} total objects."
                : $"Found {groups.Count} allocator groups totaling {totalObjects:N0} objects.",
            TopConsumers = topN.Select(g => new InsightEntry
            {
                Label = g.Name ?? "(unknown)",
                Value = $"{g.Objects?.Count() ?? 0:N0} objects",
            }).ToList(),
        };

        if (topN.Count > 0)
        {
            insights.RecommendedActions.Add("Review the top allocating methods for optimization opportunities.");
            insights.RecommendedActions.Add("Use `get_query_detail` on groups to see the individual objects allocated by each method.");
        }

        return insights;
    }

    private QueryInsights GenerateGenericInsights(string queryName, List<object> rows, string? filter)
    {
        return new QueryInsights
        {
            Summary = filter != null
                ? $"Query '{queryName}' filtered by '{filter}': {rows.Count} results."
                : $"Query '{queryName}' returned {rows.Count} results.",
        };
    }

    private static string FormatSize(long bytes)
    {
        return bytes switch
        {
            >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F2} GB",
            >= 1_048_576 => $"{bytes / 1_048_576.0:F2} MB",
            >= 1_024 => $"{bytes / 1_024.0:F2} KB",
            _ => $"{bytes} B",
        };
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
            return value;
        return value[..(maxLength - 3)] + "...";
    }
}
