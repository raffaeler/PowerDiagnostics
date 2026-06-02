using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

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

    public DebuggingSessionService(
        ILogger<DebuggingSessionService> logger,
        IHostApplicationLifetime applicationLifetime,
        IHubContext<DiagnosticHub> diagnosticHubContext,
        InvestigationState investigationState)
    {
        _logger = logger;
        applicationLifetime.ApplicationStopping.Register(() => _quit.Set());
        _diagnosticHubContext = diagnosticHubContext;
        _investigationState = investigationState;
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
        var sessionId = _investigationState.AddSnapshot(analyzer);
        await _diagnosticHubContext.Clients.All.SendAsync("onSessionCreated", new { sessionId = sessionId.ToString(), kind = "Snapshot" });
        return sessionId;
    }

    public async Task<Guid> Dump(int pid)
    {
        var analyzer = DiagnosticAnalyzer.FromDump(pid);
        var sessionId = _investigationState.AddDump(analyzer);
        await _diagnosticHubContext.Clients.All.SendAsync("onSessionCreated", new { sessionId = sessionId.ToString(), kind = "Dump" });
        return sessionId;
    }

    /// <summary>Opens a dump file from a server-side path.</summary>
    public async Task<Guid> OpenDumpFromFile(string serverPath)
    {
        var analyzer = DiagnosticAnalyzer.FromDump(serverPath, cacheObjects: true);
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
        var sessionId = _investigationState.AddDumpFromFile(analyzer, new FileInfo(path));
        await _diagnosticHubContext.Clients.All.SendAsync("onSessionCreated", new { sessionId = sessionId.ToString(), kind = "Dump" });
        _logger.LogInformation("Uploaded dump {Name}, session {Id}", fileName, sessionId);
        return sessionId;
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
            Size = (long)resolved.Size,
            BytesBase64 = Convert.ToBase64String(bytes),
        };
    }

    /// <summary>Returns GC root paths, streaming progress via SignalR.</summary>
    public async Task<GcRootPathResult?> GetGcRootPathAsync(Guid sessionId, ulong address, int maxPaths = 75)
    {
        var scope = _investigationState.GetInvestigationScope(sessionId);
        if (scope is null) return null;
        var obj = FindClrObject(scope.DiagnosticAnalyzer, address);
        if (obj is not { } resolved) return null;

        var total = scope.DiagnosticAnalyzer.GetGraphPathsCount(resolved);
        using var cts = new CancellationTokenSource();
        var lastPct = 0;
        var sid = sessionId.ToString();
        var addr = $"0x{address:X16}";

        var text = await scope.DiagnosticAnalyzer.PrintRootsAsync(resolved, pct =>
        {
            if (pct > lastPct)
            {
                lastPct = pct;
                // Fire-and-forget: progress callback is synchronous, we must not block the trace pipeline.
                _ = _diagnosticHubContext.Clients.All.SendAsync("onGcRootProgress",
                    new { sessionId = sid, objectAddress = addr, percent = pct, status = $"Processing... {pct}%" });
            }
        }, cts.Token);

        var result = new GcRootPathResult { TotalPaths = 1, TotalReferences = total, Paths = ParseRootPaths(text) };
        await _diagnosticHubContext.Clients.All.SendAsync("onGcRootComplete", new { sessionId = sid, objectAddress = addr, pathCount = total });
        return result;
    }

    /// <summary>Runs a query and returns QueryResult with metadata.</summary>
    public async Task<QueryResult?> GetQueryResultAsync(Guid sessionId, KnownQuery query, string? filter)
    {
        var scope = _investigationState.GetInvestigationScope(sessionId);
        if (scope is null) return null;
        return await Task.Run(() => query.ToQueryResult(scope.DiagnosticAnalyzer, filter));
    }

    private static ClrObject? FindClrObject(DiagnosticAnalyzer analyzer, ulong address)
    {
        foreach (var obj in analyzer.Objects)
            if (obj.Address == address) return obj;
        return null;
    }

    private static List<GcRootPathNode> ParseRootPaths(string text)
    {
        var nodes = new List<GcRootPathNode>();
        if (string.IsNullOrWhiteSpace(text)) return nodes;
        var depth = 0;
        GcRootPathNode? cur = null;
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var t = line.Trim();
            if (t.Length == 0) continue;
            if (line.StartsWith("     ") || line.StartsWith("\t"))
            {
                if (cur is not null) cur.ReferencingObjects.Add(ParseRefLine(t));
                continue;
            }
            cur = new GcRootPathNode
            {
                ObjectAddress = ExtractAddr(t) ?? "0x0000000000000000",
                TypeName = t.Split(' ').LastOrDefault() ?? "Unknown",
                RootKind = t.StartsWith("Root ") ? t.Split(' ').ElementAtOrDefault(1) ?? "Unknown" : "Reference",
                Depth = depth++,
            };
            nodes.Add(cur);
        }
        return nodes;
    }

    private static GcReferenceInfo ParseRefLine(string line)
    {
        var p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var isStatic = line.Contains("[static]");
        var fi = Array.FindIndex(p, x => x.StartsWith("field:"));
        return new GcReferenceInfo
        {
            Address = p.Length > 0 ? p[0] : "",
            TypeName = p.Length > 1 ? p[1] : "Unknown",
            FieldName = fi >= 0 ? p[fi].Replace("field:", "") : "",
            IsStatic = isStatic,
        };
    }

    private static string? ExtractAddr(string line)
    {
        var i = line.IndexOf("Addr:", StringComparison.OrdinalIgnoreCase);
        if (i >= 0) { var e = line.IndexOf(' ', i + 5); return line[(i + 5)..(e < 0 ? line.Length : e)]; }
        var p = line.Split(' ')[0];
        return p.StartsWith("0x") ? p : null;
    }

    public IList<InvestigationScope> GetActiveSessions()
        => _investigationState.GetActiveSessions();

    public InvestigationScope? GetInvestigationScope(Guid sessionId)
        => _investigationState.GetInvestigationScope(sessionId);
}

