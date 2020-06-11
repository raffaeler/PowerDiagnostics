using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

using ClrDiagnostics.Extensions;

using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;


namespace ClrDiagnostics.Triggers
{
    public abstract class TriggerBase : IDisposable
    {
        private bool _isDisposed;
        private DiagnosticsClient _client;
        private EventPipeSession _session;
        private EventPipeEventSource _source;

        public TriggerBase(int processId)
        {
            _client = new DiagnosticsClient(processId);
        }

        public bool IsStarted { get; private set; }
        public Func<TraceEvent, bool> Filter { get; private set; }
        public Action<TraceEvent> Trigger { get; private set; }

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

        public bool Start(Action<TraceEvent> trigger, Func<TraceEvent, bool> filter = null)
        {
            if (IsStarted || Providers.Count == 0) return false;

            this.Trigger = trigger ?? throw new ArgumentNullException(nameof(trigger));
            this.Filter = filter;

            //foreach (var provider in Providers)
            //{
            //    var listener = new EventListener();
            //    EventListener.EnableEvents()
            //}

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

        protected virtual void OnSubscribe(EventPipeEventSource source)
        {
        }

        protected virtual void OnEvent(TraceEvent traceEvent,
            IDictionary<string, object> payload)
        {
        }

        private void Dynamic_All(TraceEvent traceEvent)
        {
            if (Filter != null && !Filter(traceEvent)) return;
            OnEvent(traceEvent, traceEvent.GetPayload());
        }

        #region Dispose pattern
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~TriggerBase()
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
