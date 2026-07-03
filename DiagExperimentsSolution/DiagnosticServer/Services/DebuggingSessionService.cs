using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

using ClrDiagnostics;
using ClrDiagnostics.Triggers;

using CustomEventSource;

using DiagnosticInvestigations;
using DiagnosticInvestigations.Configurations;

using DiagnosticModels;
using DiagnosticModels.Converters;

using DiagnosticServer.Hubs;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using ClrDiagnostics.Experimental;

namespace DiagnosticServer.Services;
public class DebuggingSessionService : BackgroundService
{
    private readonly ILogger<DebuggingSessionService> _logger;
    private readonly IHubContext<DiagnosticHub> _diagnosticHubContext;
    private readonly InvestigationState _investigationState;
    private readonly GeneralConfiguration _generalConfiguration;
    private readonly JsonSerializerOptions _jsonOptions;

    private AutoResetEvent _quit = new(false);
    private AutoResetEvent _go = new(false);
    private Thread _worker;
    private TimeSpan _loopTimeout = TimeSpan.FromSeconds(15);

    private ConcurrentQueue<(InvestigationScope scope, KnownQuery query, TaskCompletionSource<IEnumerable>)> _executionQuery = new();

    private TriggerAll? _triggerAll;

    public DebuggingSessionService(
        ILogger<DebuggingSessionService> logger,
        IHostApplicationLifetime applicationLifetime,
        IHubContext<DiagnosticHub> diagnosticHubContext,
        InvestigationState investigationState,
        IConfiguration configuration,
        IOptions<GeneralConfiguration> generalConfigurationOption)
    {
        _logger = logger;
        applicationLifetime.ApplicationStopping.Register(() => _quit.Set());
        _diagnosticHubContext = diagnosticHubContext;
        _investigationState = investigationState;
        _generalConfiguration = generalConfigurationOption.Value;
        _jsonOptions = SetupConverters.CreateOptions();
        _worker = new(Worker);
        _worker.IsBackground = true;
        _worker.Priority = ThreadPriority.BelowNormal;
        _worker.Start();
    }

    public override void Dispose()
    {
        UnsubscribeTriggers();
        base.Dispose();
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation($"Service Started");
        return Task.CompletedTask;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation($"Service Stopped");
        return base.StopAsync(cancellationToken);
    }

    /// <summary>
    /// Worker thread loop. Runs with BelowNormal priority.
    /// Exceptions are logged rather than crashing the process.
    /// </summary>
    private async void Worker()
    {
        _logger.LogInformation($"Worker Started");
        try
        {
            WaitHandle[] handles = new[] { _quit, _go };
            (InvestigationScope scope, KnownQuery query, TaskCompletionSource<IEnumerable> tcs) trio = default;
            while (true)
            {
                //await Task.Delay(1000);
                var wait = WaitHandle.WaitAny(handles, _loopTimeout);
                if (wait == 0)
                {
                    _logger.LogInformation($"Quitting worker thread");
                    return;
                }

                if (wait == 1)
                {
                    while (_executionQuery.TryDequeue(out trio))
                    {
                        Debug.WriteLine($"Worker thread> processing query {trio.query.Name}");

                        var analyzer = trio.scope.DiagnosticAnalyzer;
                        var knownQuery = trio.query;
                        var tcs = trio.tcs;
                        var result = knownQuery.Populate!(analyzer);
                        tcs.SetResult(result);
                    }

                    continue;
                }

                _investigationState.ClearSessionIfExpired();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Worker thread crashed");
        }
    }

    public Task<IEnumerable> ExecuteAsync(InvestigationScope scope, KnownQuery query)
    {
        var tcs = new TaskCompletionSource<IEnumerable>();
        _executionQuery.Enqueue((scope, query, tcs));
        _go.Set();
        return tcs.Task;
    }

    /// <summary>
    /// Sends a trigger event to connected clients.
    /// Fire-and-forget by design: trace event delegates are synchronous Action&lt;T&gt;
    /// and we must not block the trace event source.
    /// </summary>
    private void SendTrigger(EvsBase evs)
    {
        evs.Timestamp = DateTime.UtcNow;
        var evsJson = JsonSerializer.Serialize(evs, _jsonOptions);
        _ = _diagnosticHubContext.Clients.All.SendAsync("onEvs", evsJson);
    }

    public void SubscribeTriggers(int pid)
    {
        UnsubscribeTriggers();
        _triggerAll = new TriggerAll(pid,
            Constants.CustomHeaderEventSourceName,
            Constants.TriggerHeaderCounterName);

        _triggerAll.OnCpu = d => SendTrigger(new EvsCpu(d));
        _triggerAll.OnEventCounterCount = d => SendTrigger(new EvsCustomHeader(d));
        _triggerAll.OnException = d => SendTrigger(new EvsException(d));
        _triggerAll.OnGcAllocation = d => SendTrigger(new EvsGcAllocation(d));
        _triggerAll.OnHttpRequests = d => SendTrigger(new EvsHttpRequests(d));
        _triggerAll.OnWorkingSet = d => SendTrigger(new EvsWorkingSet(d));

        _triggerAll.Start();
    }

    public void UnsubscribeTriggers()
    {
        if (_triggerAll != null)
        {
            _triggerAll.Dispose();
            _triggerAll = null;
        }
    }

    public async Task<string> Snapshot(int pid)
    {
        var analyzer = DiagnosticAnalyzer.FromSnapshot(pid);
        PrepareDiagnosticAnalyzer(analyzer);
        var sessionId = _investigationState.AddSnapshot(analyzer);
        await _diagnosticHubContext.Clients.All.SendAsync("onSessionCreated", new { sessionId, kind = "Snapshot" });
        return sessionId;
    }

    public async Task<string> Dump(int pid)
    {
        var analyzer = DiagnosticAnalyzer.FromDump(pid);
        PrepareDiagnosticAnalyzer(analyzer);
        var sessionId = _investigationState.AddDump(analyzer);
        await _diagnosticHubContext.Clients.All.SendAsync("onSessionCreated", new { sessionId, kind = "Dump" });
        return sessionId;
    }

    /// <summary>Opens a dump file from a server-side path. The session ID is derived from the filename.</summary>
    public async Task<string> OpenDumpFromFile(string serverPath)
    {
        var analyzer = DiagnosticAnalyzer.FromDump(serverPath, cacheObjects: true);
        PrepareDiagnosticAnalyzer(analyzer);
        var sessionId = _investigationState.AddDumpFromFile(analyzer, new FileInfo(serverPath));
        _logger.LogInformation("Opened dump from {Path}, session {Id}", serverPath, sessionId);
        await _diagnosticHubContext.Clients.All.SendAsync("onSessionCreated", new { sessionId, kind = "Dump" });
        return sessionId;
    }

    /// <summary>Opens an uploaded dump, saving to temp file first. The session ID is derived from the original filename.</summary>
    public async Task<string> OpenDumpFromUploadAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        var dir = Path.Combine(Path.GetTempPath(), "PowerDiagnostics");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{Guid.NewGuid()}_{fileName}");
        await using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            await stream.CopyToAsync(fs, ct);
        var analyzer = DiagnosticAnalyzer.FromDump(path, cacheObjects: true);
        PrepareDiagnosticAnalyzer(analyzer);
        var sessionId = _investigationState.AddDumpFromFile(analyzer, new FileInfo(fileName));
        await _diagnosticHubContext.Clients.All.SendAsync("onSessionCreated", new { sessionId, kind = "Dump" });
        _logger.LogInformation("Uploaded dump {Name}, session {Id}", fileName, sessionId);
        return sessionId;
    }

    /// <summary>Closes a session, disposing analyzer and temp files.</summary>
    public async Task CloseSession(string sessionId)
    {
        _investigationState.RemoveSession(sessionId);
        _logger.LogInformation("Closed session {Id}", sessionId);
        await _diagnosticHubContext.Clients.All.SendAsync("onSessionClosed", new { sessionId });
    }

    /// <summary>Returns raw bytes for a heap object (hex viewer).</summary>
    public HexDataResult? GetHexData(string sessionId, ulong address)
    {
        var scope = _investigationState.GetInvestigationScope(sessionId);
        if (scope is null) return null;
        var obj = FindClrObject(scope.DiagnosticAnalyzer, address);
        if (obj is not { } resolved) return null;
        var bytes = scope.DiagnosticAnalyzer.ReadRawContent(resolved);
        return new HexDataResult
        {
            ObjectAddress = $"0x{address:X16}",
            TypeName = resolved.Type?.Name ?? "Unknown",
            Mt = resolved.Type is not null ? $"0x{resolved.Type.MethodTable:X16}" : string.Empty,
            Size = (long)resolved.Size,
            BytesBase64 = Convert.ToBase64String(bytes),
        };
    }

    /// <summary>Returns GC root paths, streaming progress via SignalR.</summary>
    public async Task<GcRootPathResult?> GetGcRootPathAsync(string sessionId, ulong address, int maxPaths = -1)
    {
        var scope = _investigationState.GetInvestigationScope(sessionId);
        if (scope is null) return null;
        var obj = FindClrObject(scope.DiagnosticAnalyzer, address);
        if (obj is not { } resolved) return null;

        using var cts = new CancellationTokenSource();
        var lastSentCount = 0;
        var addr = $"0x{address:X16}";

        var result = await scope.DiagnosticAnalyzer.GetRootPathsAsync(resolved, count =>
        {
            if (count - lastSentCount >= 100)
            {
                lastSentCount = count;
                // Fire-and-forget: progress callback is synchronous, we must not block the trace pipeline.
                _ = _diagnosticHubContext.Clients.All.SendAsync("onGcRootProgress",
                    new { sessionId, objectAddress = addr, count = count, status = $"Processing... {count}" });
            }
        }, cts.Token, maxPaths);

        await _diagnosticHubContext.Clients.All.SendAsync("onGcRootComplete",
            new { sessionId, objectAddress = addr, pathCount = result.TotalReferences });
        return result;
    }

    /// <summary>Returns GC root paths passing through a target address, walking up to roots
    /// and down to referenced objects, streaming progress via SignalR.</summary>
    public async Task<GcRootPathResult?> GetAddressPathAsync(string sessionId, ulong address, int maxPaths = -1)
    {
        var scope = _investigationState.GetInvestigationScope(sessionId);
        if (scope is null) return null;
        var obj = FindClrObject(scope.DiagnosticAnalyzer, address);
        if (obj is not { } resolved) return null;

        using var cts = new CancellationTokenSource();
        var lastSentCount = 0;
        var addr = $"0x{address:X16}";

        DiagnosticAnalyzerHelper hlp = new(scope.DiagnosticAnalyzer);
        var result = await hlp.GetAddressPathAsync(resolved.Address, count =>
        {
            if (count - lastSentCount >= 100)
            {
                lastSentCount = count;
                // Fire-and-forget: progress callback is synchronous, we must not block the trace pipeline.
                _ = _diagnosticHubContext.Clients.All.SendAsync("onAddressPathProgress",
                    new { sessionId, objectAddress = addr, count = count, status = $"Processing... {count}" });
            }
        }, cts.Token, maxPaths);

        await _diagnosticHubContext.Clients.All.SendAsync("onAddressPathComplete",
            new { sessionId, objectAddress = addr, pathCount = result.TotalReferences });
        return result;
    }

    /// <summary>Runs a query and returns QueryResult with metadata, streaming progress via SignalR.</summary>
    public async Task<QueryResult?> GetQueryResultAsync(
        string sessionId,
        KnownQuery query,
        string? filter,
        CancellationToken cancellationToken = default)
    {
        var scope = _investigationState.GetInvestigationScope(sessionId);
        if (scope is null) return null;

        var lastSentCount = 0;
        var queryName = query.Name ?? "Query";

        var result = await query.ToQueryResultAsync(
            scope.DiagnosticAnalyzer,
            filter,
            count =>
            {
                if (count - lastSentCount >= 10)
                {
                    lastSentCount = count;
                    // Fire-and-forget: progress callback is synchronous, we must not block.
                    _ = _diagnosticHubContext.Clients.All.SendAsync("onQueryProgress",
                        new { sessionId, queryName = queryName, count = count, status = $"Processing... {count}" });
                }
            },
            cancellationToken);

        await _diagnosticHubContext.Clients.All.SendAsync("onQueryComplete",
            new { sessionId, queryName = queryName, rowCount = result.Rows.Cast<object>().Count() });
        return result;
    }

    /// <summary>
    /// Returns all heap objects for a specific MethodTable address.
    /// Runs the DumpHeapStat query internally and locates the matching MT entry.
    /// </summary>
    public MethodTableResult? GetMethodTableObjects(string sessionId, ulong mt)
    {
        var scope = _investigationState.GetInvestigationScope(sessionId);
        if (scope is null) return null;

        var stats = scope.DiagnosticAnalyzer.DumpHeapStat(0);
        foreach (var (type, objects, size) in stats)
        {
            if (type?.MethodTable == mt)
            {
                return new MethodTableResult
                {
                    Mt = $"0x{mt:X16}",
                    TypeName = type.Name,
                    GraphSize = size,
                    ObjectCount = objects.Count,
                    Objects = objects,
                };
            }
        }

        return null; // MT not found
    }

    // ──────────────────────── Memory Map ────────────────────────

    /// <summary>
    /// Returns the memory segment layout of the GC heap for a session.
    /// </summary>
    public IEnumerable<MemorySegmentInfo>? GetMemoryMap(string sessionId)
    {
        var scope = _investigationState.GetInvestigationScope(sessionId);
        return scope?.DiagnosticAnalyzer.GetMemorySegments();
    }

    // ──────────────────────── Raw Memory Read ────────────────────────

    /// <summary>
    /// Reads raw bytes at an arbitrary address, with region partitioning
    /// showing which object owns each byte range.
    /// </summary>
    public RawMemoryResult? GetRawMemory(string sessionId, ulong address, int length = 512)
    {
        var scope = _investigationState.GetInvestigationScope(sessionId);
        if (scope is null) return null;

        var analyzer = scope.DiagnosticAnalyzer;
        var bytes = analyzer.ReadRawMemory(address, length);
        if (bytes.Length == 0)
        {
            return new RawMemoryResult
            {
                Address = $"0x{address:X16}",
                Length = 0,
                BytesBase64 = string.Empty,
                RegionKind = "Unmapped",
            };
        }

        var regions = analyzer.PartitionMemoryRange(address, bytes.Length);
        var firstOwner = regions.FirstOrDefault(r => r.ObjectAddress is not null);

        string regionKind = "HeapObject";
        if (regions.All(r => r.Kind == "FreeBlock")) regionKind = "FreeBlock";
        else if (regions.All(r => r.Kind == "Unmapped")) regionKind = "Unmapped";

        return new RawMemoryResult
        {
            Address = $"0x{address:X16}",
            Length = bytes.Length,
            BytesBase64 = Convert.ToBase64String(bytes),
            RegionKind = regionKind,
            Regions = regions,
            ContainingObjectAddress = firstOwner?.ObjectAddress,
            ContainingObjectTypeName = firstOwner?.ObjectTypeName,
            OffsetWithinObject = firstOwner?.OffsetWithinObject,
        };
    }

    // ──────────────────────── Object Field Layout ────────────────────────

    /// <summary>
    /// Returns the field layout of a heap object with reference annotations.
    /// </summary>
    public ObjectFieldLayout? GetObjectLayout(string sessionId, ulong address)
    {
        var scope = _investigationState.GetInvestigationScope(sessionId);
        return scope?.DiagnosticAnalyzer.GetObjectFieldLayout(address);
    }

    // ──────────────────────── Data Owner ────────────────────────

    /// <summary>
    /// Returns the containing object (data owner) for any address, plus
    /// referencing objects if the address is an object start.
    /// </summary>
    public DataOwnerResult? GetDataOwner(string sessionId, ulong address)
    {
        var scope = _investigationState.GetInvestigationScope(sessionId);
        if (scope is null) return null;

        var analyzer = scope.DiagnosticAnalyzer;
        var containing = analyzer.FindContainingObject(address);

        if (containing is not { } c)
        {
            // Address is not within any known object
            return new DataOwnerResult
            {
                Address = $"0x{address:X16}",
                Kind = "Unmapped",
            };
        }

        bool isObjectStart = c.ContainingObject.Address == address;

        return new DataOwnerResult
        {
            Address = $"0x{address:X16}",
            Kind = isObjectStart ? "ObjectStart" : "InsideObject",
            ContainingObjectAddress = $"0x{c.ContainingObject.Address:X16}",
            ContainingObjectTypeName = c.ContainingObject.Type?.Name ?? "Unknown",
            OffsetWithinObject = c.OffsetWithinObject,
            ObjectSize = (long)c.ContainingObject.Size,
            IsObjectStart = isObjectStart,
            ReferencingObjects = isObjectStart
                ? analyzer.GetReferencingObjects(address).ToList()
                : null,
        };
    }

    /// <summary>
    /// Returns the list of objects that hold references to the given object address.
    /// Only meaningful for object-start addresses.
    /// </summary>
    public ReferencingObjectsResult? GetReferencingObjects(string sessionId, ulong address)
    {
        var scope = _investigationState.GetInvestigationScope(sessionId);
        if (scope is null) return null;

        var analyzer = scope.DiagnosticAnalyzer;
        bool isObjectStart = analyzer.FindContainingObject(address) is { } c
            && c.ContainingObject.Address == address;

        var refs = isObjectStart
            ? analyzer.GetReferencingObjects(address).ToList()
            : new List<GcReferenceInfo>();

        return new ReferencingObjectsResult
        {
            TargetAddress = $"0x{address:X16}",
            IsObjectStart = isObjectStart,
            ReferencingObjects = refs,
        };
    }

    /// <summary>
    /// Combined address info: data owner + field layout + referencing objects.
    /// </summary>
    public AddressInfoResult? GetAddressInfo(string sessionId, ulong address)
    {
        var scope = _investigationState.GetInvestigationScope(sessionId);
        if (scope is null) return null;

        var analyzer = scope.DiagnosticAnalyzer;
        var containing = analyzer.FindContainingObject(address);

        if (containing is not { } c)
        {
            return new AddressInfoResult
            {
                Address = $"0x{address:X16}",
                Kind = "Unmapped",
            };
        }

        bool isObjectStart = c.ContainingObject.Address == address;

        return new AddressInfoResult
        {
            Address = $"0x{address:X16}",
            Kind = isObjectStart ? "ObjectStart" : "InsideObject",
            ContainingObjectAddress = $"0x{c.ContainingObject.Address:X16}",
            ContainingObjectTypeName = c.ContainingObject.Type?.Name ?? "Unknown",
            OffsetWithinObject = c.OffsetWithinObject,
            ObjectSize = (long)c.ContainingObject.Size,
            IsObjectStart = isObjectStart,
            FieldLayout = isObjectStart
                ? analyzer.GetObjectFieldLayout(address)
                : null,
            ReferencingObjects = isObjectStart
                ? analyzer.GetReferencingObjects(address).ToList()
                : null,
        };
    }

    private static ClrObject? FindClrObject(DiagnosticAnalyzer analyzer, ulong address)
    {
        foreach (var obj in analyzer.Objects)
            if (obj.Address == address) return obj;
        return null;
    }

    public IList<InvestigationScope> GetActiveSessions()
        => _investigationState.GetActiveSessions();

    public InvestigationScope? GetInvestigationScope(string sessionId)
        => _investigationState.GetInvestigationScope(sessionId);

    /// <summary>
    /// Centralizes setting the settings specific to the DiagnosticAnalyzer
    /// </summary>
    /// <param name="diagnosticAnalyzer"></param>
    private void PrepareDiagnosticAnalyzer(DiagnosticAnalyzer diagnosticAnalyzer)
    {
        diagnosticAnalyzer.ApplyNet10DatasStaticWorkaround =
            _generalConfiguration.AnalyzerSettings.ApplyNet10DatasStaticWorkaround;
    }
}

