using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Text;
using System.Linq;

using CustomEventSource;

using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using System.Diagnostics;

namespace TestConsole.Triggers
{
    public class TriggerOnCustomHeader : IDisposable
    {
        private DiagnosticsClient _client;
        private IList<EventPipeProvider> _providers = new List<EventPipeProvider>();
        private EventPipeSession _session;
        private EventPipeEventSource _source;

        public TriggerOnCustomHeader(int processId)
        {
            _client = new DiagnosticsClient(processId);
            _providers.Add(new EventPipeProvider(
                Constants.CustomHeaderEventSourceName,
                EventLevel.Verbose, -1));
        }

        public void Dispose()
        {
            Stop();
        }

        public void Start()
        {
            Stop();

            _session = _client.StartEventPipeSession(_providers);
            _source = new EventPipeEventSource(_session.EventStream);
            _source.AllEvents += SourceAllEvents;
            _source.Dynamic.All += Dynamic_All;
            _source.Process();
        }

        private void SourceAllEvents(TraceEvent obj)
        {
            var textual = obj.Dump(true);
            
            //Activity activity = new Activity("myOperation");
            //EventWrittenEventArgs args = obj. as EventWrittenEventArgs;

            var dict = obj.GetPayload();
            if (dict == null) return;
            int count = (int)dict["Count"];
            var max = (double)dict["Max"];
            Console.WriteLine($"{obj.EventName} - {count} - {max}");
        }

        private void Dynamic_All(TraceEvent obj)
        {
            //Microsoft.Diagnostics.Tracing.Parsers.ClrTraceEventParser

            var dict = obj.GetPayload();
            if (dict == null) return;
            int count = (int)dict["Count"];
            var max = (double)dict["Max"];

            Console.WriteLine($"{obj.EventName} - {count} - {max}");
        }

        public void Stop()
        {
            //_source.StopProcessing();

            if (_source != null) { _source.Dispose(); _source = null; }
            if (_session != null) { _session.Dispose(); _session = null; }
        }
    }
}
