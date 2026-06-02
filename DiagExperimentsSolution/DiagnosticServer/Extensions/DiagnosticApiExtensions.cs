using ClrDiagnostics.Helpers;

using DiagnosticInvestigations;

using DiagnosticServer.Services;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace DiagnosticServer.Extensions;

/// <summary>
/// Extension methods for mapping the diagnostic Minimal API endpoints.
/// </summary>
public static class DiagnosticApiExtensions
{
    /// <summary>
    /// Maps all diagnostic API endpoints to the provided <see cref="IEndpointRouteBuilder"/>.
    /// </summary>
    public static IEndpointRouteBuilder MapDiagnosticApi(this IEndpointRouteBuilder endpoints)
    {
        MapProcessEndpoints(endpoints);
        MapSessionEndpoints(endpoints);
        return endpoints;
    }

    // ──────────────────────────── Process Endpoints ────────────────────────────

    private static void MapProcessEndpoints(IEndpointRouteBuilder endpoints)
    {
        /// <summary>
        /// Returns a list of all dotnet running processes.
        /// </summary>
        endpoints.MapGet("/api/processes", () =>
        {
            var processes = ProcessHelper.Default.GetDotnetProcesses()
                .Select(p => new { Id = p.Id, Name = p.ProcessName });
            return Results.Ok(processes);
        })
        .WithName("GetProcesses")
        .Produces<IEnumerable<object>>();

        /// <summary>
        /// Attaches event triggers to the specified process.
        /// </summary>
        endpoints.MapPost("/api/processes/attach/{id}", (int id, DebuggingSessionService debuggingSessionService) =>
        {
            debuggingSessionService.SubscribeTriggers(id);
            return Results.Ok();
        })
        .WithName("AttachEvents")
        .Produces(StatusCodes.Status200OK);

        /// <summary>
        /// Detaches all event triggers.
        /// </summary>
        endpoints.MapPost("/api/processes/detach", (DebuggingSessionService debuggingSessionService) =>
        {
            debuggingSessionService.UnsubscribeTriggers();
            return Results.Ok();
        })
        .WithName("DetachEvents")
        .Produces(StatusCodes.Status200OK);

        /// <summary>
        /// Takes a snapshot of the specified process.
        /// </summary>
        endpoints.MapPost("/api/processes/snapshot/{id}", (int id, DebuggingSessionService debuggingSessionService) =>
        {
            var sessionId = debuggingSessionService.Snapshot(id);
            return Results.Ok(sessionId);
        })
        .WithName("SnapshotProcess")
        .Produces<Guid>();

        /// <summary>
        /// Creates a dump of the specified process.
        /// </summary>
        endpoints.MapPost("/api/processes/dump/{id}", (int id, DebuggingSessionService debuggingSessionService) =>
        {
            var sessionId = debuggingSessionService.Dump(id);
            return Results.Ok(sessionId);
        })
        .WithName("DumpProcess")
        .Produces<Guid>();
    }

    // ──────────────────────────── Session Endpoints ────────────────────────────

    private static void MapSessionEndpoints(IEndpointRouteBuilder endpoints)
    {
        /// <summary>
        /// Returns a list of all active diagnostic sessions.
        /// </summary>
        endpoints.MapGet("/api/sessions", (DebuggingSessionService debuggingSessionService) =>
        {
            var sessions = debuggingSessionService.GetActiveSessions()
                .Select(d => new
                {
                    d.SessionId,
                    d.InvestigationKind,
                    d.Created,
                });
            return Results.Ok(sessions);
        })
        .WithName("GetSessions")
        .Produces<IEnumerable<object>>();

        /// <summary>
        /// Returns the list of available query names.
        /// </summary>
        endpoints.MapGet("/api/sessions/queries", (QueriesService queriesService) =>
        {
            return Results.Ok(queriesService.Queries.Keys);
        })
        .WithName("GetQueries")
        .Produces<IEnumerable<string>>();

        /// <summary>
        /// Runs a diagnostic query against the specified session.
        /// Offloads the execution to a background worker thread to avoid blocking.
        /// </summary>
        endpoints.MapPost("/api/sessions/{sessionId}/{query}",
            async (string sessionId, string query,
                   DebuggingSessionService debuggingSessionService,
                   QueriesService queriesService,
                   ILogger<Program> logger) =>
        {
            if (!queriesService.Queries.TryGetValue(query, out var knownQuery))
            {
                return Results.NotFound(new ProblemDetails
                {
                    Title = "Query not found",
                    Detail = $"The query '{query}' is not available.",
                    Status = StatusCodes.Status404NotFound,
                });
            }

            if (!Guid.TryParse(sessionId, out Guid id))
            {
                return Results.NotFound(new ProblemDetails
                {
                    Title = "Invalid session ID",
                    Detail = $"'{sessionId}' is not a valid session ID.",
                    Status = StatusCodes.Status404NotFound,
                });
            }

            var scope = debuggingSessionService.GetInvestigationScope(id);
            if (scope == null)
            {
                return Results.NotFound(new ProblemDetails
                {
                    Title = "Session not found",
                    Detail = $"No active session found with ID '{sessionId}'.",
                    Status = StatusCodes.Status404NotFound,
                });
            }

            // Offload the execution to the background service thread via TaskCompletionSource.
            System.Collections.IEnumerable result;
            logger.LogInformation("Offloaded the execution of the query {QueryName}", knownQuery.Name);
            try
            {
                result = await debuggingSessionService.ExecuteAsync(scope, knownQuery);
                logger.LogInformation("Query {QueryName} has completed", knownQuery.Name);
            }
            catch (Exception err)
            {
                logger.LogError(err, "Query {QueryName} has faulted", knownQuery.Name);
                throw;
            }

            return Results.Ok(result);
        })
        .WithName("RunQuery")
        .Produces<System.Collections.IEnumerable>()
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
