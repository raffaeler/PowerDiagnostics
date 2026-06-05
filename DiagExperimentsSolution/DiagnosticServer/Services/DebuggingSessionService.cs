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

using DiagnosticModels;
using DiagnosticModels.Converters;

using DiagnosticServer.Hubs;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

namespace DiagnosticServer.Services;
public class DebuggingSessionService : BackgroundService
{
    private readonly ILogger<DebuggingSessionService> _logger;
    private readonly IHubContext<DiagnosticHub> _diagnosticHubContext;
    private readonly InvestigationState _investigationState;
    private readonly JsonSerializerOptions _jsonOptions;

    private AutoResetEvent _quit = new(false);
    private AutoResetEvent _go = new(false);
    private Thread _worker;
    private TimeSpan _loopTimeout = TimeSpan.FromSeconds(15);

    private ConcurrentQueue<(InvestigationScope scope, KnownQuery query, TaskCompletionSource<IEnumerable>)> _executionQuery = new();

    private TriggerAll? _triggerAll;

    private readonly bool _deduplicateRegisterRoots;

    public DebuggingSessionService(
        ILogger<DebuggingSessionService> logger,
        IHostApplicationLifetime applicationLifetime,
        IHubContext<DiagnosticHub> diagnosticHubContext,
        InvestigationState investigationState,
        IConfiguration configuration)
    {
        _logger = logger;
        applicationLifetime.ApplicationStopping.Register(() => _quit.Set());
        _diagnosticHubContext = diagnosticHubContext;
        _investigationState = investigationState;
        _deduplicateRegisterRoots = configuration.GetValue<bool>("Diagnostics:DeduplicateRegisterRoots");
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

    public async Task<Guid> Snapshot(int pid)
    {
        var analyzer = DiagnosticAnalyzer.FromSnapshot(pid);
        ConfigureAnalyzer(analyzer);
        var sessionId = _investigationState.AddSnapshot(analyzer);
        await _diagnosticHubContext.Clients.All.SendAsync("onSessionCreated", new { sessionId = sessionId.ToString(), kind = "Snapshot" });
        return sessionId;
    }

    public async Task<Guid> Dump(int pid)
    {
        var analyzer = DiagnosticAnalyzer.FromDump(pid);
        ConfigureAnalyzer(analyzer);
        var sessionId = _investigationState.AddDump(analyzer);
        await _diagnosticHubContext.Clients.All.SendAsync("onSessionCreated", new { sessionId = sessionId.ToString(), kind = "Dump" });
        return sessionId;
    }

    /// <summary>Opens a dump file from a server-side path.</summary>
    public async Task<Guid> OpenDumpFromFile(string serverPath)
    {
        var analyzer = DiagnosticAnalyzer.FromDump(serverPath, cacheObjects: true);
        ConfigureAnalyzer(analyzer);
        var sessionId = _investigationState.AddDump(analyzer);
        _logger.LogInformation("Opened dump from {Path}, session {Id}", serverPath, sessionId);
        await _diagnosticHubContext.Clients.All.SendAsync("onSessionCreated", new { sessionId = sessionId.ToString(), kind = "Dump" });
        return sessionId;
    }

    /// <summary>Opens an uploaded dump, saving to temp file first.</summary>
    public async Task<Guid> OpenDumpFromUploadAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        var dir = Path.Combine(Path.GetTempPath(), "PowerDiagnostics");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{Guid.NewGuid()}_{fileName}");
        await using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            await stream.CopyToAsync(fs, ct);
        var analyzer = DiagnosticAnalyzer.FromDump(path, cacheObjects: true);
        ConfigureAnalyzer(analyzer);
        var sessionId = _investigationState.AddDumpFromFile(analyzer, new FileInfo(path));
        await _diagnosticHubContext.Clients.All.SendAsync("onSessionCreated", new { sessionId = sessionId.ToString(), kind = "Dump" });
        _logger.LogInformation("Uploaded dump {Name}, session {Id}", fileName, sessionId);
        return sessionId;
    }

    private void ConfigureAnalyzer(DiagnosticAnalyzer analyzer)
    {
        analyzer.DeduplicateRegisterRoots = _deduplicateRegisterRoots;
    }

    /// <summary>Closes a session, disposing analyzer and temp files.</summary>
    public async Task CloseSession(Guid sessionId)
    {
        _investigationState.RemoveSession(sessionId);
        _logger.LogInformation("Closed session {Id}", sessionId);
        await _diagnosticHubContext.Clients.All.SendAsync("onSessionClosed", new { sessionId = sessionId.ToString() });
    }

    /// <summary>Returns raw bytes for a heap object (hex viewer).</summary>
    public HexDataResult? GetHexData(Guid sessionId, ulong address)
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
    public async Task<GcRootPathResult?> GetGcRootPathAsync(Guid sessionId, ulong address, int maxPaths = -1)
    {
        var scope = _investigationState.GetInvestigationScope(sessionId);
        if (scope is null) return null;
        var obj = FindClrObject(scope.DiagnosticAnalyzer, address);
        if (obj is not { } resolved) return null;

        using var cts = new CancellationTokenSource();
        var lastSentCount = 0;
        var sid = sessionId.ToString();
        var addr = $"0x{address:X16}";

        var result = await scope.DiagnosticAnalyzer.GetRootPathsAsync(resolved, count =>
        {
            if (count - lastSentCount >= 100)
            {
                lastSentCount = count;
                // Fire-and-forget: progress callback is synchronous, we must not block the trace pipeline.
                _ = _diagnosticHubContext.Clients.All.SendAsync("onGcRootProgress",
                    new { sessionId = sid, objectAddress = addr, count = count, status = $"Processing... {count}" });
            }
        }, cts.Token, maxPaths);

        await _diagnosticHubContext.Clients.All.SendAsync("onGcRootComplete",
            new { sessionId = sid, objectAddress = addr, pathCount = result.TotalReferences });
        return result;
    }

    /// <summary>Runs a query and returns QueryResult with metadata, streaming progress via SignalR.</summary>
    public async Task<QueryResult?> GetQueryResultAsync(
        Guid sessionId,
        KnownQuery query,
        string? filter,
        CancellationToken cancellationToken = default)
    {
        var scope = _investigationState.GetInvestigationScope(sessionId);
        if (scope is null) return null;

        var lastSentCount = 0;
        var sid = sessionId.ToString();
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
                        new { sessionId = sid, queryName = queryName, count = count, status = $"Processing... {count}" });
                }
            },
            cancellationToken);

        await _diagnosticHubContext.Clients.All.SendAsync("onQueryComplete",
            new { sessionId = sid, queryName = queryName, rowCount = result.Rows.Cast<object>().Count() });
        return result;
    }

    /// <summary>
    /// Returns all heap objects for a specific MethodTable address.
    /// Runs the DumpHeapStat query internally and locates the matching MT entry.
    /// </summary>
    public MethodTableResult? GetMethodTableObjects(Guid sessionId, ulong mt)
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

    private static ClrObject? FindClrObject(DiagnosticAnalyzer analyzer, ulong address)
    {
        foreach (var obj in analyzer.Objects)
            if (obj.Address == address) return obj;
        return null;
    }

    public IList<InvestigationScope> GetActiveSessions()
        => _investigationState.GetActiveSessions();

    public InvestigationScope? GetInvestigationScope(Guid sessionId)
        => _investigationState.GetInvestigationScope(sessionId);
}

