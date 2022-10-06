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

namespace ClrDiagnostics.Triggers
{
    public class TriggerAll
    {
        private bool _isDisposed;
        private DiagnosticsClient _client;
        private EventPipeSession _session;
        private EventPipeEventSource _source;
        private string _eventCounterName;

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
            long keywords = 0, IDictionary<string, string> parameters = null)
        {
            if (!KnownProviders.TryGetName(name, out string knownName))
            {
                throw new Exception($"Unknown provider {knownName}");
            }

            AddProvider(knownName, eventLevel, keywords, parameters);
        }

        public void AddProvider(string name, EventLevel eventLevel = EventLevel.Informational,
            long keywords = 0, IDictionary<string, string> parameters = null)
        {
            Providers.Add(new EventPipeProvider(name, eventLevel, keywords, parameters));
        }

        public bool Start() 
        {
            if (IsStarted || Providers.Count == 0) return false;

            Task.Run(() =>
            {
                _session = _client.StartEventPipeSession(Providers, false);
                _source = new EventPipeEventSource(_session.EventStream);
                OnSubscribe(_source);
                _source.Dynamic.All += Dynamic_All;
                _source.Process();
            });

            IsStarted = true;
            return true;
        }

        public bool Stop()
        {
            if (!IsStarted) return false;

            if (_source != null) { _source.Dispose(); _source = null; }
            if (_session != null) { _session.Dispose(); _session = null; }

            IsStarted = false;
            return true;
        }

        public Action<double> OnCpu { get; set; }
        public Action<double> OnGcAllocation { get; set; }
        public Action<double> OnWorkingSet { get; set; }
        public Action<double> OnEventCounterCount { get; set; }
        public Action<double> OnHttpRequests { get; set; }
        public Action<string> OnException { get; set; }

        protected virtual void OnSubscribe(EventPipeEventSource source)
        {
            source.Clr.GCAllocationTick += traceEvent =>
            {
                var obj = traceEvent as GCAllocationTickTraceData;
                //Debug.WriteLine($"{obj.ClrInstanceID} - {obj.TypeName} - {obj.AllocationAmount} - {obj.AllocationKind}");
                OnGcAllocation?.Invoke(obj.AllocationAmount);
            };

            source.Clr.ExceptionStart += traceEvent =>
            {
                var obj = traceEvent as ExceptionTraceData;
                //OnException?.Invoke(obj.)
                var text = $"{obj.ExceptionType}: {obj.ExceptionMessage}";
                OnException?.Invoke(text);
            };
        }

        protected virtual void OnEvent(TraceEvent traceEvent,
            IDictionary<string, object> payload)
        {
            if (payload == null) return;
            var name = payload["Name"]?.ToString();

            if (name == "cpu-usage")
            {
                var cpuUsage = (double)payload["Mean"];
                OnCpu?.Invoke(cpuUsage);
            }


            if (name == "working-set")
            {
                var mean = (double)payload["Mean"];
                var units = payload["DisplayUnits"].ToString();
                OnWorkingSet?.Invoke(mean);
            }


            // event counter
            if (name == _eventCounterName)
            {
                int count = (int)payload["Count"];
                OnEventCounterCount?.Invoke(count);
            }

            // http req
            if (name == "requests-per-second")
            {
                var increment = (double)payload["Increment"];
                OnHttpRequests?.Invoke(increment);
            }

        }

        private void Dynamic_All(TraceEvent traceEvent)
        {
            OnEvent(traceEvent, traceEvent.GetPayload());
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
}
