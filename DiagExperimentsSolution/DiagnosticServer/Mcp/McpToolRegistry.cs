using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

using DiagnosticInvestigations;
using DiagnosticModels.Converters;

using ModelContextProtocol.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DiagnosticServer.Mcp;

/// <summary>
/// Manages tool registration for the MCP server.
/// All tools (Tier 1 + Tier 2) are registered at startup.
/// Tier 2 diagnostic tools return helpful error messages when no dump session is active,
/// guiding the AI to use `list_dumps` and `open_dump` first.
/// </summary>
public class McpToolRegistry
{
    /// <summary>
    /// JSON serializer options with the custom ClrMD converters.
    /// Used to round-trip ClrMD types to JSON-safe objects, avoiding
    /// serialization errors on non-serializable types (e.g., ReadOnlySpan
    /// on ClrStackFrame.Context).
    /// </summary>
    public static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        MaxDepth = 512,
        TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(),
        Converters =
        {
            new ClrExceptionConverter(),
            new ClrInstanceFieldConverter(),
            new ClrModuleConverter(),
            new ClrObjectConverter(),
            new ClrRootConverter(),
            new ClrStackFrameConverter(),
            new ClrStaticFieldConverter(),
            new ClrThreadConverter(),
            new ClrTypeConverter(),
        },
    };

    private readonly ILogger<McpToolRegistry> _logger;

    public McpToolRegistry(ILogger<McpToolRegistry> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Converts a ClrMD object to a JSON-safe representation by round-tripping
    /// through serialization with the custom ClrMD converters.
    /// </summary>
    public static object? ToJsonSafeObject(object? row)
    {
        if (row is null)
            return null;

        try
        {
            var json = JsonSerializer.Serialize(row, row.GetType(), JsonOptions);
            return JsonSerializer.Deserialize<System.Text.Json.Nodes.JsonNode>(json);
        }
        catch (Exception)
        {
            return new { type = row.GetType().FullName, value = row.ToString() ?? "(null)" };
        }
    }

    /// <summary>
    /// Helper to check if any session is active and throw a helpful error if not.
    /// Called by Tier 2 tools before they execute.
    /// Returns the non-null scope to satisfy nullable analysis.
    /// </summary>
    public static InvestigationScope ValidateSession(McpSessionManager sessionManager, string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new InvalidOperationException(
                "No session ID provided. Use `list_dumps` to see available dumps, then `open_dump` to open one. Diagnostic tools are only available after a dump is opened.");

        var scope = sessionManager.GetInvestigationScope(sessionId);
        if (scope == null)
            throw new InvalidOperationException(
                $"Session '{sessionId}' not found. Use `list_sessions` to see active sessions, or `open_dump` to open a dump. Diagnostic tools are only available after a dump is opened.");

        return scope;
    }
}

