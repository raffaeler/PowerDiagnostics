using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Text;
using Microsoft.Diagnostics.Tracing;

namespace ClrDiagnostics.Triggers
{
    public class TriggerOnHttpRequests : TriggerBase
    {
        public TriggerOnHttpRequests(int processId, int threshold) : base(processId)
        {
            this.Threshold = threshold;

            KnownProviders.TryGetName(
                KnownProviderName.Microsoft_AspNetCore_Hosting, out string aspnetHostingProvider);
            this.AddProvider(aspnetHostingProvider, EventLevel.Informational, 0,
                new Dictionary<string, string>() { { "EventCounterIntervalSec", "1" } });
        }

        public int Threshold { get; }


        // Shape of the aspnet-hosting trace event:
        // {{
        //  Name:"requests-per-second",
        //  DisplayName:"Request Rate",
        //  DisplayRateTimeScale:"00:00:01",
        //  Increment:0,
        //  IntervalSec:1.0119962,
        //  Metadata:"",
        //  Series:"Interval=1000",
        //  CounterType:"Sum",
        //  DisplayUnits:""
        // }}
        //
        //
        // {{
        //  Name:"current-requests",
        //  DisplayName:"Current Requests",
        //  Mean:0,
        //  StandardDeviation:0,
        //  Count:1,
        //  Min:0,
        //  Max:0,
        //  IntervalSec:1.0036477,
        //  Series:"Interval=1000",
        //  CounterType:"Mean",
        //  Metadata:"",
        //  DisplayUnits:""
        // }}

        protected override void OnEvent(TraceEvent traceEvent, IDictionary<string, object> payload)
        {
            if (payload == null) return;

            Trigger(traceEvent);
        }

    }
}
