using System.Threading;

using ClrDiagnostics.Helpers;

using DiagnosticInvestigations;
using DiagnosticInvestigations.Configurations;
using DiagnosticModels;
using DiagnosticModels.Converters;

using DiagnosticServer.Services;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

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
        endpoints.MapPost("/api/processes/snapshot/{id}", async (int id, DebuggingSessionService debuggingSessionService) =>
        {
            var sessionId = await debuggingSessionService.Snapshot(id);
            return Results.Ok(sessionId);
        })
        .WithName("SnapshotProcess")
        .Produces<string>();

        /// <summary>
        /// Creates a dump of the specified process.
        /// </summary>
        endpoints.MapPost("/api/processes/dump/{id}", async (int id, DebuggingSessionService debuggingSessionService) =>
        {
            var sessionId = await debuggingSessionService.Dump(id);
            return Results.Ok(sessionId);
        })
        .WithName("DumpProcess")
        .Produces<string>();
    }

    // ──────────────────────────── Session Endpoints ────────────────────────────

    private static void MapSessionEndpoints(IEndpointRouteBuilder endpoints)
    {
        /// <summary>
        /// Returns a list of all .dmp and .mdmp files in the configured dumps folder.
        /// </summary>
        endpoints.MapGet("/api/sessions/dumps", (IOptions<GeneralConfiguration> config) =>
        {
            var folder = config.Value.DumpsFolder;
            if (!Directory.Exists(folder))
            {
                return Results.Ok(Array.Empty<string>());
            }

            var dmpFiles = Directory.EnumerateFiles(folder, "*.dmp").Select(Path.GetFileName);
            var mdmpFiles = Directory.EnumerateFiles(folder, "*.mdmp").Select(Path.GetFileName);
            var allFiles = dmpFiles.Concat(mdmpFiles).OrderBy(f => f).ToArray();
            return Results.Ok(allFiles);
        })
        .WithName("GetDumps")
        .Produces<string[]>();

        /// <summary>
        /// Opens a dump file from the configured dumps folder.
        /// </summary>
        endpoints.MapPost("/api/sessions/open-dump-path", async (DumpPathRequest request, DebuggingSessionService svc, IOptions<GeneralConfiguration> config) =>
        {
            if (string.IsNullOrWhiteSpace(request.Path))
            {
                return Results.BadRequest(new ProblemDetails
                {
                    Title = "Invalid path",
                    Detail = "The dump file path must not be empty.",
                    Status = StatusCodes.Status400BadRequest,
                });
            }

            var dumpsFolder = Path.GetFullPath(config.Value.DumpsFolder);
            var resolved = Path.GetFullPath(Path.Combine(dumpsFolder, request.Path));

            if (!resolved.StartsWith(dumpsFolder, StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new ProblemDetails
                {
                    Title = "Invalid path",
                    Detail = "The dump file must be inside the configured dumps folder.",
                    Status = StatusCodes.Status400BadRequest,
                });
            }

            if (!File.Exists(resolved))
            {
                return Results.NotFound(new ProblemDetails
                {
                    Title = "File not found",
                    Detail = $"The file '{request.Path}' does not exist in the dumps folder.",
                    Status = StatusCodes.Status404NotFound,
                });
            }

            var sessionId = await svc.OpenDumpFromFile(resolved);
            return Results.Ok(new { sessionId, investigationKind = InvestigationKind.Dump.ToString(), created = DateTime.Now });
        })
        .WithName("OpenDumpPath")
        .Produces<object>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

        /// <summary>
        /// Opens an uploaded dump file.
        /// </summary>
        endpoints.MapPost("/api/sessions/open-dump", async (HttpRequest request, DebuggingSessionService svc, CancellationToken ct) =>
        {
            if (!request.HasFormContentType)
            {
                return Results.BadRequest(new ProblemDetails
                {
                    Title = "Invalid content type",
                    Detail = "Expected multipart/form-data with a file field named 'file'.",
                    Status = StatusCodes.Status400BadRequest,
                });
            }

            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("file");
            if (file is null || file.Length == 0)
            {
                return Results.BadRequest(new ProblemDetails
                {
                    Title = "No file uploaded",
                    Detail = "Please upload a .dmp file in the 'file' field.",
                    Status = StatusCodes.Status400BadRequest,
                });
            }

            await using var stream = file.OpenReadStream();
            var sessionId = await svc.OpenDumpFromUploadAsync(stream, file.FileName, ct);
            return Results.Ok(new { sessionId, investigationKind = InvestigationKind.Dump.ToString(), created = DateTime.Now });
        })
        .WithName("OpenDumpUpload")
        .Produces<object>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .DisableAntiforgery();

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
        /// Returns metadata for all queries including column definitions.
        /// </summary>
        endpoints.MapGet("/api/sessions/queries/metadata", (QueriesService queriesService) =>
        {
            var metadata = queriesService.Queries.Values.Select(q => q.GetMetadata()).ToList();
            return Results.Ok(metadata);
        })
        .WithName("GetQueriesMetadata")
        .Produces<IEnumerable<QueryMetadata>>();

        /// <summary>
        /// Runs a diagnostic query against the specified session.
        /// Returns QueryResult with HasDetails/DetailType metadata.
        /// </summary>
        endpoints.MapPost("/api/sessions/{sessionId}/{query}",
            async (string sessionId, string query, string? filter,
                   DebuggingSessionService debuggingSessionService,
                   QueriesService queriesService,
                   ILogger<Program> logger,
                   CancellationToken cancellationToken) =>
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

            logger.LogInformation("Running query {QueryName} on session {SessionId}", query, sessionId);
            var result = await debuggingSessionService.GetQueryResultAsync(sessionId, knownQuery, filter, cancellationToken);

            if (result is null)
            {
                return Results.NotFound(new ProblemDetails
                {
                    Title = "Session not found",
                    Detail = $"No active session found with ID '{sessionId}'.",
                    Status = StatusCodes.Status404NotFound,
                });
            }

            return Results.Ok(result);
        })
        .WithName("RunQuery")
        .Produces<QueryResult>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        /// <summary>
        /// Gets detailed information for a heap object at the given address (hex data + metadata).
        /// </summary>
        endpoints.MapPost("/api/sessions/{sessionId}/address/{objectAddress}",
            (string sessionId, string objectAddress, DebuggingSessionService svc) =>
        {
            if (!TryParseHexAddress(objectAddress, out var addr))
                return Results.BadRequest(new ProblemDetails { Title = "Invalid address", Detail = $"'{objectAddress}' is not a valid hex address.", Status = StatusCodes.Status400BadRequest });

            var result = svc.GetHexData(sessionId, addr);
            if (result is null)
                return Results.NotFound(new ProblemDetails { Title = "Object not found", Detail = $"No object at address '{objectAddress}' in session.", Status = StatusCodes.Status404NotFound });

            return Results.Ok(result);
        })
        .WithName("GetAddressData")
        .Produces<HexDataResult>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        /// <summary>
        /// Returns all heap objects for a specific MethodTable address.
        /// </summary>
        endpoints.MapPost("/api/sessions/{sessionId}/methodTable/{mt}",
            (string sessionId, string mt, DebuggingSessionService svc) =>
        {
            if (!TryParseHexAddress(mt, out var mtAddr))
                return Results.BadRequest(new ProblemDetails { Title = "Invalid MT address", Detail = $"'{mt}' is not a valid hex address.", Status = StatusCodes.Status400BadRequest });

            var result = svc.GetMethodTableObjects(sessionId, mtAddr);
            if (result is null)
                return Results.NotFound(new ProblemDetails { Title = "MethodTable not found", Detail = $"No objects found for MT '{mt}' in session.", Status = StatusCodes.Status404NotFound });

            return Results.Ok(result);
        })
        .WithName("GetMethodTableObjects")
        .Produces<MethodTableResult>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        /// <summary>
        /// Gets GC root paths for a heap object.
        /// </summary>
        endpoints.MapPost("/api/sessions/{sessionId}/gcroot/{objectAddress}",
            async (string sessionId, string objectAddress, int? maxPaths, DebuggingSessionService svc) =>
        {
            if (!TryParseHexAddress(objectAddress, out var addr))
                return Results.BadRequest(new ProblemDetails { Title = "Invalid address", Detail = $"'{objectAddress}' is not a valid hex address.", Status = StatusCodes.Status400BadRequest });

            var result = await svc.GetGcRootPathAsync(
                sessionId, addr, maxPaths ?? -1);
            if (result is null)
                return Results.NotFound(new ProblemDetails { Title = "Object not found", Detail = $"No object at address '{objectAddress}' in session.", Status = StatusCodes.Status404NotFound });

            return Results.Ok(result);
        })
        .WithName("GetGcRootPath")
        .Produces<GcRootPathResult>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        /// <summary>
        /// Gets GC root paths passing through a target address, walking both up to roots
        /// and down to referenced objects.
        /// </summary>
        endpoints.MapPost("/api/sessions/{sessionId}/addresspath/{objectAddress}",
            async (string sessionId, string objectAddress, int? maxPaths, DebuggingSessionService svc) =>
        {
            if (!TryParseHexAddress(objectAddress, out var addr))
                return Results.BadRequest(new ProblemDetails { Title = "Invalid address", Detail = $"'{objectAddress}' is not a valid hex address.", Status = StatusCodes.Status400BadRequest });

            var result = await svc.GetAddressPathAsync(
                sessionId, addr, maxPaths ?? -1);
            if (result is null)
                return Results.NotFound(new ProblemDetails { Title = "Object not found", Detail = $"No object at address '{objectAddress}' in session.", Status = StatusCodes.Status404NotFound });

            return Results.Ok(result);
        })
        .WithName("GetAddressPath")
        .Produces<GcRootPathResult>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        /// <summary>
        /// Returns the memory segment layout of the GC heap for a session.
        /// </summary>
        endpoints.MapGet("/api/sessions/{sessionId}/memorymap",
            (string sessionId, DebuggingSessionService svc) =>
        {
            var result = svc.GetMemoryMap(sessionId);
            if (result is null)
                return Results.NotFound(new ProblemDetails { Title = "Session not found", Detail = "No active session found.", Status = StatusCodes.Status404NotFound });

            return Results.Ok(result);
        })
        .WithName("GetMemoryMap")
        .Produces<IEnumerable<MemorySegmentInfo>>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        /// <summary>
        /// Reads raw bytes at an arbitrary address in the dump, with region
        /// partitioning showing which object owns each byte range.
        /// </summary>
        endpoints.MapPost("/api/sessions/{sessionId}/memory/{address}",
            (string sessionId, string address, int? length, DebuggingSessionService svc) =>
        {
            if (!TryParseHexAddress(address, out var addr))
                return Results.BadRequest(new ProblemDetails { Title = "Invalid address", Detail = $"'{address}' is not a valid hex address.", Status = StatusCodes.Status400BadRequest });

            var result = svc.GetRawMemory(sessionId, addr, length ?? 512);
            if (result is null)
                return Results.NotFound(new ProblemDetails { Title = "Session not found", Detail = "No active session found.", Status = StatusCodes.Status404NotFound });

            return Results.Ok(result);
        })
        .WithName("GetRawMemory")
        .Produces<RawMemoryResult>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        /// <summary>
        /// Returns the field layout of a heap object with reference annotations.
        /// </summary>
        endpoints.MapPost("/api/sessions/{sessionId}/layout/{address}",
            (string sessionId, string address, DebuggingSessionService svc) =>
        {
            if (!TryParseHexAddress(address, out var addr))
                return Results.BadRequest(new ProblemDetails { Title = "Invalid address", Detail = $"'{address}' is not a valid hex address.", Status = StatusCodes.Status400BadRequest });

            var result = svc.GetObjectLayout(sessionId, addr);
            if (result is null)
                return Results.NotFound(new ProblemDetails { Title = "Object not found", Detail = $"No object at address '{address}'.", Status = StatusCodes.Status404NotFound });

            return Results.Ok(result);
        })
        .WithName("GetObjectLayout")
        .Produces<ObjectFieldLayout>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        /// <summary>
        /// Returns the containing object (data owner) for any address.
        /// Includes referencing objects when the address is an object start.
        /// </summary>
        endpoints.MapPost("/api/sessions/{sessionId}/dataowner/{address}",
            (string sessionId, string address, DebuggingSessionService svc) =>
        {
            if (!TryParseHexAddress(address, out var addr))
                return Results.BadRequest(new ProblemDetails { Title = "Invalid address", Detail = $"'{address}' is not a valid hex address.", Status = StatusCodes.Status400BadRequest });

            var result = svc.GetDataOwner(sessionId, addr);
            if (result is null)
                return Results.NotFound(new ProblemDetails { Title = "Session not found", Detail = "No active session found.", Status = StatusCodes.Status404NotFound });

            return Results.Ok(result);
        })
        .WithName("GetDataOwner")
        .Produces<DataOwnerResult>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        /// <summary>
        /// Returns objects that hold references to the given object address.
        /// Only meaningful for object-start addresses; returns IsObjectStart=false for interior addresses.
        /// </summary>
        endpoints.MapPost("/api/sessions/{sessionId}/referencing/{address}",
            (string sessionId, string address, DebuggingSessionService svc) =>
        {
            if (!TryParseHexAddress(address, out var addr))
                return Results.BadRequest(new ProblemDetails { Title = "Invalid address", Detail = $"'{address}' is not a valid hex address.", Status = StatusCodes.Status400BadRequest });

            var result = svc.GetReferencingObjects(sessionId, addr);
            if (result is null)
                return Results.NotFound(new ProblemDetails { Title = "Session not found", Detail = "No active session found.", Status = StatusCodes.Status404NotFound });

            return Results.Ok(result);
        })
        .WithName("GetReferencingObjects")
        .Produces<ReferencingObjectsResult>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        /// <summary>
        /// Returns objects directly referenced by the given object (1 level forward walk).
        /// Each result node includes the field name that references it.
        /// </summary>
        endpoints.MapPost("/api/sessions/{sessionId}/referenced/{address}",
            (string sessionId, string address, DebuggingSessionService svc) =>
        {
            if (!TryParseHexAddress(address, out var addr))
                return Results.BadRequest(new ProblemDetails { Title = "Invalid address", Detail = $"'{address}' is not a valid hex address.", Status = StatusCodes.Status400BadRequest });

            var result = svc.GetForwardReferences(sessionId, addr);
            if (result is null)
                return Results.NotFound(new ProblemDetails { Title = "Session not found", Detail = "No active session found.", Status = StatusCodes.Status404NotFound });

            return Results.Ok(result);
        })
        .WithName("GetForwardReferences")
        .Produces<GcRootPathResult>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        /// <summary>
        /// Combined address info: data owner + field layout + referencing objects.
        /// </summary>
        endpoints.MapPost("/api/sessions/{sessionId}/addressinfo/{address}",
            (string sessionId, string address, DebuggingSessionService svc) =>
        {
            if (!TryParseHexAddress(address, out var addr))
                return Results.BadRequest(new ProblemDetails { Title = "Invalid address", Detail = $"'{address}' is not a valid hex address.", Status = StatusCodes.Status400BadRequest });

            var result = svc.GetAddressInfo(sessionId, addr);
            if (result is null)
                return Results.NotFound(new ProblemDetails { Title = "Session not found", Detail = "No active session found.", Status = StatusCodes.Status404NotFound });

            return Results.Ok(result);
        })
        .WithName("GetAddressInfo")
        .Produces<AddressInfoResult>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        /// <summary>
        /// Returns the lightweight module list for a session.
        /// </summary>
        endpoints.MapGet("/api/sessions/{sessionId}/modules",
            (string sessionId, DebuggingSessionService svc) =>
        {
            var modules = svc.GetModules(sessionId);
            if (modules is null)
                return Results.NotFound(new ProblemDetails
                {
                    Title = "Session not found",
                    Detail = "No active session found.",
                    Status = StatusCodes.Status404NotFound,
                });

            return Results.Ok(modules);
        })
        .WithName("GetModules")
        .Produces<IEnumerable<ModuleDataLight>>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        /// <summary>
        /// Returns full detail for a single module (includes PDB info, architecture, etc.).
        /// </summary>
        endpoints.MapPost("/api/sessions/{sessionId}/modules/{moduleName}/detail",
            (string sessionId, string moduleName, DebuggingSessionService svc) =>
        {
            if (string.IsNullOrWhiteSpace(moduleName))
                return Results.BadRequest(new ProblemDetails
                {
                    Title = "Invalid module name",
                    Detail = "Module name must not be empty.",
                    Status = StatusCodes.Status400BadRequest,
                });

            var detail = svc.GetModuleDetail(sessionId, moduleName);
            if (detail is null)
                return Results.NotFound(new ProblemDetails
                {
                    Title = "Module not found",
                    Detail = $"No module matching '{moduleName}' was found in this session.",
                    Status = StatusCodes.Status404NotFound,
                });

            return Results.Ok(detail);
        })
        .WithName("GetModuleDetail")
        .Produces<ModuleDataDetail>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        /// <summary>
        /// Decompiles a managed module using ilspycmd and returns C# source.
        /// Requires ilspycmd to be installed on the server.
        /// </summary>
        endpoints.MapPost("/api/sessions/{sessionId}/modules/{moduleName}/decompile",
            async (string sessionId, string moduleName, DebuggingSessionService svc) =>
        {
            if (string.IsNullOrWhiteSpace(moduleName))
                return Results.BadRequest(new ProblemDetails
                {
                    Title = "Invalid module name",
                    Detail = "Module name must not be empty.",
                    Status = StatusCodes.Status400BadRequest,
                });

            var source = await svc.DecompileModuleAsync(sessionId, moduleName);
            if (source is null)
                return Results.Problem(new ProblemDetails
                {
                    Title = "Decompilation failed",
                    Detail = $"Could not decompile '{moduleName}'. The module may not be managed, may not exist, " +
                             "or ilspycmd may not be installed on the server.",
                    Status = StatusCodes.Status501NotImplemented,
                });

            return Results.Ok(new { moduleName, source });
        })
        .WithName("DecompileModule")
        .Produces<object>()
        .ProducesProblem(StatusCodes.Status501NotImplemented)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        /// <summary>
        /// Closes a diagnostic session and releases resources.
        /// </summary>
        endpoints.MapDelete("/api/sessions/{sessionId}",
            async (string sessionId, DebuggingSessionService svc) =>
        {
            await svc.CloseSession(sessionId);
            return Results.Ok();
        })
        .WithName("CloseSession")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static bool TryParseHexAddress(string hex, out ulong value)
    {
        hex = hex.Trim();
        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            hex = hex[2..];
        return ulong.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out value);
    }
}

/// <summary>
/// Request body for opening a dump from a server-side path.
/// </summary>
public record DumpPathRequest(string Path);
