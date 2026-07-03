using System.ComponentModel;

using Microsoft.Extensions.Options;

using ModelContextProtocol.Server;

namespace DiagnosticServer.Mcp;

/// <summary>
/// Tier 1 MCP tools — always registered. These serve as the entry point for the AI:
/// listing dumps, opening a dump (which triggers Tier 2 registration), listing sessions,
/// and closing sessions (which triggers Tier 2 cleanup).
/// </summary>
[McpServerToolType]
public class McpSessionTools
{
    private readonly McpSessionManager _sessionManager;
    private readonly ILogger<McpSessionTools> _logger;

    public McpSessionTools(McpSessionManager sessionManager, ILogger<McpSessionTools> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    /// <summary>
    /// Lists all available dump files in the configured dumps folder.
    /// Use this first to discover what dumps are available for analysis.
    /// </summary>
    [McpServerTool(Name = "list_dumps")]
    [Description("List all available memory dump files (.dmp, .mdmp) in the configured dumps folder. Use this to discover what dumps are available for diagnostic analysis.")]
    public List<string> ListDumps()
    {
        _logger.LogInformation("MCP: list_dumps called");
        return _sessionManager.ListDumps();
    }

    /// <summary>
    /// Opens a dump file by name and prepares it for diagnostic queries.
    /// On success, additional diagnostic tools become available for querying the dump.
    /// Returns session metadata including the target framework, heap object count,
    /// and a catalog of available diagnostic tools.
    /// </summary>
    [McpServerTool(Name = "open_dump")]
    [Description("Open a memory dump file by name from the configured dumps folder. On success, returns session metadata including target framework, object count, and a catalog of available diagnostic tools. NOTE: The set of available tools changes based on session state — additional diagnostic tools become available after opening a dump.")]
    public async Task<OpenDumpResult> OpenDump(
        [Description("The filename of the dump to open (e.g., 'mydump.dmp'). Must exist in the configured dumps folder.")] string dumpName)
    {
        _logger.LogInformation("MCP: open_dump called with dumpName={DumpName}", dumpName);

        try
        {
            var result = await _sessionManager.OpenDumpAsync(dumpName);
            return result;
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException(ex.Message, ex);
        }
        catch (FileNotFoundException ex)
        {
            throw new InvalidOperationException(ex.Message, ex);
        }
    }

    /// <summary>
    /// Lists all currently active diagnostic sessions.
    /// </summary>
    [McpServerTool(Name = "list_sessions")]
    [Description("List all currently active diagnostic sessions with their kind (Dump/Snapshot) and creation time.")]
    public List<object> ListSessions()
    {
        _logger.LogInformation("MCP: list_sessions called");
        var sessions = _sessionManager.GetActiveSessions();
        return sessions.Select(s => new
        {
            sessionId = s.SessionId,
            kind = s.InvestigationKind.ToString(),
            created = s.Created,
        }).ToList<object>();
    }

    /// <summary>
    /// Closes a diagnostic session and releases its resources.
    /// If this was the last session, diagnostic tools are unregistered.
    /// </summary>
    [McpServerTool(Name = "close_session")]
    [Description("Close a diagnostic session by its ID and release associated resources. If this is the last active session, diagnostic query and inspection tools are unregistered.")]
    public async Task<string> CloseSession(
        [Description("The session ID to close (returned by open_dump).")] string sessionId)
    {
        _logger.LogInformation("MCP: close_session called with sessionId={SessionId}", sessionId);

        try
        {
            await _sessionManager.CloseSessionAsync(sessionId);
            return $"Session '{sessionId}' closed successfully.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing session {SessionId}", sessionId);
            throw new InvalidOperationException($"Failed to close session '{sessionId}': {ex.Message}", ex);
        }
    }
}
