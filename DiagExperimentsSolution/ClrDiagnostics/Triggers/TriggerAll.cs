using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Text;
using System.Threading.Tasks;
using ClrDiagnostics.Extensions;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;

namespace ClrDiagnostics.Triggers;
public class TriggerAll
{
    private bool _isDisposed;
    private DiagnosticsClient _client;
    private EventPipeSession? _session;
    private EventPipeEventSource? _source;
    private string? _eventCounterName;
    private Task? _processingTask;

    public TriggerAll(int processId, string eventSourceName, string eventCounterName)
    {
        _client = new DiagnosticsClient(processId);
        _eventCounterName = eventCounterName;

        this.AddProvider("System.Runtime",
            EventLevel.Informational,
            (long)ClrTraceEventParser.Keywords.None,
            new Dictionary<string, string>() { { "EventCounterIntervalSec", "1" } });

        if (eventSourceName != null)
        {
            this.AddProvider(eventSourceName, EventLevel.Verbose, -1,
                new Dictionary<string, string>() { { "EventCounterIntervalSec", "1" } });
        }

        this.AddProvider("Microsoft-Windows-DotNETRuntime",
            EventLevel.Verbose, -1);

        this.AddProvider("Microsoft-DotNETCore-SampleProfiler",
            EventLevel.Verbose, (long)ClrTraceEventParser.Keywords.All);


        KnownProviders.TryGetName(
            KnownProviderName.Microsoft_AspNetCore_Hosting, out string aspnetHostingProvider);
        this.AddProvider(aspnetHostingProvider, EventLevel.Informational, 0,
            new Dictionary<string, string>() { { "EventCounterIntervalSec", "1" } });

    }

    public bool IsStarted { get; private set; }

    protected IList<EventPipeProvider> Providers { get; private set; } = new List<EventPipeProvider>();

    public void AddKnownProvider(KnownProviderName name,
        EventLevel eventLevel = EventLevel.Informational,
        long keywords = 0, IDictionary<string, string>? parameters = null)
    {
        if (!KnownProviders.TryGetName(name, out string knownName))
        {
            throw new Exception($"Unknown provider {knownName}");
        }

        AddProvider(knownName, eventLevel, keywords, parameters);
    }

    public void AddProvider(string name, EventLevel eventLevel = EventLevel.Informational,
        long keywords = 0, IDictionary<string, string>? parameters = null)
    {
        Providers.Add(new EventPipeProvider(name, eventLevel, keywords, parameters));
    }

    public bool Start() 
    {
        if (IsStarted || Providers.Count == 0) return false;

        _processingTask = Task.Run(() =>
        {
            try
            {
                _session = _client.StartEventPipeSession(Providers, false);
                _source = new EventPipeEventSource(_session.EventStream);
                OnSubscribe(_source);
                _source.Dynamic.All += Dynamic_All;
                _source.Process();
            }
            catch (Exception) when (_isDisposed)
            {
                // Expected when the session is stopped during processing
            }
            catch (Exception ex)
            {
                // Log unexpected errors that would otherwise crash the
                // background task silently (unobserved task exception).
                // The task is fire-and-forget by design.
                Debug.WriteLine($"[TriggerAll] Event processing failed: {ex}");
            }
        });

        IsStarted = true;
        return true;
    }

    public bool Stop()
    {
        if (!IsStarted) return false;

        // Dispose the session first — this stops the EventPipe and causes
        // _source.Process() to return on the background thread.
        if (_session != null) { _session.Dispose(); _session = null; }

        // Wait for the processing task to finish before disposing the source.
        // No timeout needed: disposing the session closes the stream, which
        // guarantees Process() returns (either normally or via exception).
        if (_processingTask != null)
        {
            try { _processingTask.Wait(); }
            catch (AggregateException) { /* expected if Process() threw */ }
            _processingTask = null;
        }

        if (_source != null) { _source.Dispose(); _source = null; }

        IsStarted = false;
        return true;
    }

    public Action<double>? OnCpu { get; set; }
    public Action<double>? OnGcAllocation { get; set; }
    public Action<double>? OnWorkingSet { get; set; }
    public Action<double>? OnEventCounterCount { get; set; }
    public Action<double>? OnHttpRequests { get; set; }
    public Action<string>? OnException { get; set; }

    protected virtual void OnSubscribe(EventPipeEventSource source)
    {
        source.Clr.GCAllocationTick += traceEvent =>
        {
            if (traceEvent is not GCAllocationTickTraceData obj) return;
            OnGcAllocation?.Invoke(obj.AllocationAmount);
        };

        source.Clr.ExceptionStart += traceEvent =>
        {
            if (traceEvent is not ExceptionTraceData obj) return;
            var text = $"{obj.ExceptionType}: {obj.ExceptionMessage}";
            OnException?.Invoke(text);
        };
    }

    protected virtual void OnEvent(TraceEvent traceEvent,
        IDictionary<string, object> payload)
    {
        if (payload == null) return;
        if (!payload.TryGetValue("Name", out var nameObj)) return;
        var name = nameObj?.ToString();

        if (name == "cpu-usage" && payload.TryGetValue("Mean", out var cpuMean))
        {
            OnCpu?.Invoke((double)cpuMean);
        }

        if (name == "working-set" && payload.TryGetValue("Mean", out var wsMean))
        {
            OnWorkingSet?.Invoke((double)wsMean);
        }

        // event counter
        if (name == _eventCounterName && payload.TryGetValue("Count", out var countObj))
        {
            OnEventCounterCount?.Invoke((int)countObj);
        }

        // http req
        if (name == "requests-per-second" && payload.TryGetValue("Increment", out var incObj))
        {
            OnHttpRequests?.Invoke((double)incObj);
        }
    }

    private void Dynamic_All(TraceEvent traceEvent)
    {
        OnEvent(traceEvent, traceEvent.GetPayload()!);
    }

    #region Dispose pattern
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~TriggerAll()
    {
        Dispose(false);
    }

    protected void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            if (disposing)
            {
                // Set _isDisposed BEFORE Stop() so the background task's
                // exception filter can catch the ObjectDisposedException
                // that Process() throws when the session stream closes.
                _isDisposed = true;
                OnDisposing();
                Stop();
            }

            _isDisposed = true;
        }
    }

    protected virtual void OnDisposing()
    {
    }
    #endregion

}

