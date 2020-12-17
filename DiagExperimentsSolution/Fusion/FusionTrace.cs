using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;

// dotnet-trace collect --providers Microsoft-Windows-DotNETRuntime:4:4  --  Browsing.exe

namespace Fusion
{
    public class FusionTrace : IDisposable
    {
        private static readonly string DotNetRuntimeProvider = "Microsoft-Windows-DotNETRuntime";
        private bool _isDisposed;
        private DiagnosticsClient _client;
        private EventPipeSession _session;
        private EventPipeEventSource _source;


        public FusionTrace(int processId)
        {
            _client = new DiagnosticsClient(processId);
            AddDotNetRuntimeProvider();
        }

        public FusionTrace(IpcEndpoint endpoint)
        {
            _client = new DiagnosticsClient(endpoint);
            AddDotNetRuntimeProvider();
        }

        public bool IsStarted { get; private set; }
        public Func<TraceEvent, bool> Filter { get; private set; }
        public Action<TraceEvent> Trigger { get; private set; }

        protected IList<EventPipeProvider> Providers { get; private set; } = new List<EventPipeProvider>();

        private void AddDotNetRuntimeProvider()
        {
            Providers.Add(new EventPipeProvider(DotNetRuntimeProvider, EventLevel.Informational, 4, null));
        }

        public bool Start(Action<TraceEvent> trigger, Action<EventPipeEventSource> onSubscribe, Func<TraceEvent, bool> filter = null)
        {
            if (IsStarted || Providers.Count == 0) return false;

            this.Trigger = trigger;
            this.Filter = filter;

            Task.Run(() =>
            {
                if (!StartSession())
                {
                    Console.WriteLine($"Could not start the session - aborting");
                    // TODO: better exit
                    return;
                }

                // resuming remote process
                _client.ResumeRuntime();

                _source = new EventPipeEventSource(_session.EventStream);
                onSubscribe(_source);

                _source.Dynamic.All += Dynamic_All;
                _source.Process();
            });

            IsStarted = true;
            return true;
        }

        private bool StartSession()
        {
            int retries = 20;
            while (retries > 0 && _session == null)
            {
                try
                {
                    _session = _client.StartEventPipeSession(Providers, false);
                }
                catch (ServerNotAvailableException)
                {
                }
                catch (Exception err)
                {
                    Console.WriteLine(err.Message);
                }

                retries--;
            }

            return _session != null;
        }

        public bool Stop()
        {
            if (!IsStarted) return false;

            if (_source != null) { _source.Dispose(); _source = null; }
            if (_session != null) { _session.Dispose(); _session = null; }

            IsStarted = false;
            return true;
        }

        private void Dynamic_All(TraceEvent traceEvent)
        {

            if (Filter != null && !Filter(traceEvent)) return;
            Trigger(traceEvent);
        }

        #region Dispose pattern
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~FusionTrace()
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
