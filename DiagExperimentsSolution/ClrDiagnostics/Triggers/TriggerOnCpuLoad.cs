using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;

using ClrDiagnostics.Extensions;

using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;

namespace ClrDiagnostics.Triggers
{
    public class TriggerOnCpuLoad : TriggerBase
    {
        public TriggerOnCpuLoad(int processId) : base(processId)
        {
            this.AddProvider("System.Runtime",
                EventLevel.Informational,
                (long)ClrTraceEventParser.Keywords.None,
                new Dictionary<string, string>() { { "EventCounterIntervalSec", "1" } });
        }

        private double _threshold;
        public double Threshold
        {
            get => _threshold;
            set
            {
                if (IsStarted) throw new Exception("Can't change the parameter while running");
                _threshold = value;
            }
        }

        protected override void OnEvent(TraceEvent traceEvent, IDictionary<string, object> payload)
        {
            if (payload == null) return;
            var name = payload["Name"]?.ToString();

            if (name == "cpu-usage")
            {
                var cpuUsage = (double)payload["Mean"];
                if (cpuUsage > Threshold)
                {
                    Console.WriteLine($"High Load! {cpuUsage}");
                    Trigger(traceEvent);
                }
            }

            if (name == "working-set")
            {
                var mean = (double)payload["Mean"];
                var units = payload["DisplayUnits"].ToString();
                Console.WriteLine($"{mean}{units}");

                //Trigger();
            }

        }
    }
}
