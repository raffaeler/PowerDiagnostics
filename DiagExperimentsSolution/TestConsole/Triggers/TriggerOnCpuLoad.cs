using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;

using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;

namespace TestConsole.Triggers
{
    public class TriggerOnCpuLoad : IDisposable
    {
        private DiagnosticsClient _client;
        private IList<EventPipeProvider> _providers = new List<EventPipeProvider>();
        private EventPipeSession _session;
        private EventPipeEventSource _source;

        public TriggerOnCpuLoad(int processId)
        {
            _client = new DiagnosticsClient(processId);
            _providers.Add(new EventPipeProvider(
                "System.Runtime",
                EventLevel.Informational,
                (long)ClrTraceEventParser.Keywords.None,
                new Dictionary<string, string>() { { "EventCounterIntervalSec", "1" } }
                ));
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
            _source.Dynamic.All += Dynamic_All;
            _source.Process();
        }

        private void Dynamic_All(TraceEvent obj)
        {
            var threshold = 10;
            if (obj.EventName.Equals("EventCounters"))
            {
                var fields = obj.GetPayload();
                if (fields == null) return;

                var name = fields["Name"]?.ToString();

                //Console.WriteLine($"name={name}");
                if (name == "cpu-usage")
                {
                    var cpuUsage = (double)fields["Mean"];
                    if (cpuUsage > (double)threshold)
                    {
                        Console.WriteLine($"High Load! {cpuUsage}");
                    }
                }

                if (name == "working-set")
                {
                    var mean = (double)fields["Mean"];
                    var units = fields["DisplayUnits"].ToString();
                    Console.WriteLine($"{mean}{units}");
                }
            }
        }

        public void Stop()
        {
            //_source.StopProcessing();

            if (_source != null) { _source.Dispose(); _source = null; }
            if (_session != null) { _session.Dispose(); _session = null; }
        }
    }
}
