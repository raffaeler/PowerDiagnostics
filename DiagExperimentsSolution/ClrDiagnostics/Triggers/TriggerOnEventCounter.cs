using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Text;
using System.Linq;

using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using System.Diagnostics;
using ClrDiagnostics.Extensions;

namespace ClrDiagnostics.Triggers
{
    public class TriggerOnEventCounter : TriggerBase
    {
        public TriggerOnEventCounter(int processId, string eventSourceName) : base(processId)
        {
            this.AddProvider(eventSourceName, EventLevel.Verbose, -1);
        }

        protected override void OnEvent(TraceEvent traceEvent, IDictionary<string, object> payload)
        {
            if (payload == null) return;

            string counterName = (string)payload["Name"];
            int count = (int)payload["Count"];
            var max = (double)payload["Max"];
            Console.WriteLine($"{counterName} - {count} - {max}");

            Trigger();
        }

    }
}
