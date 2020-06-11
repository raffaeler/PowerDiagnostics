using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Text;

using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;

namespace ClrDiagnostics.Triggers
{
    public class TriggerOnMemoryUsage : TriggerBase
    {
        public TriggerOnMemoryUsage(int processId) : base(processId)
        {
            this.AddProvider("Microsoft-Windows-DotNETRuntime",
                EventLevel.Verbose, -1);

            this.AddProvider("System.Runtime",
                EventLevel.Informational,
                (long)ClrTraceEventParser.Keywords.None,
                new Dictionary<string, string>() { { "EventCounterIntervalSec", "1" } });
        }

        protected override void OnSubscribe(EventPipeEventSource source)
        {
            source.Clr.GCAllocationTick += traceEvent => Trigger(traceEvent);
            //source.
        }

        protected override void OnEvent(TraceEvent traceEvent, IDictionary<string, object> payload)
        {
            if (payload == null) return;
            var name = payload["Name"]?.ToString();

            if (name == "working-set")
            {
                //var mean = (double)payload["Mean"];
                //var units = payload["DisplayUnits"].ToString();
                Trigger(traceEvent);
            }
        }

        //private void OnGCAllocationTick(
        //    Microsoft.Diagnostics.Tracing.Parsers.Clr.GCAllocationTickTraceData obj)
        //{
        //    //obj.AllocationAmount64
        //    //obj.AllocationKind
        //    //obj.ClrInstanceID
        //    //obj.TypeID
        //    //obj.TypeName
        //    //obj.HeapIndex
        //    //obj.Address

        //    Console.WriteLine($"{obj.ClrInstanceID} - {obj.TypeName} - {obj.AllocationAmount} - {obj.AllocationKind}");
        //}
    }
}
