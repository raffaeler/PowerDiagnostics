using System;
using System.Collections.Generic;
using System.Text;

using ClrDiagnostics.Extensions;
using ClrDiagnostics.Helpers;
using ClrDiagnostics.Triggers;

using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;

namespace TestConsole
{
    public class UseCase4
    {
        public void Analyze()
        {
            //var ps = ProcessHelper.GetProcess("StaticMemoryLeaks");
            var ps = ProcessHelper.GetProcess("TestAllocation");
            if (ps == null)
            {
                Console.WriteLine("Run the required process first");
                return;
            }

            //var analyzer = new TriggerOnCpuLoad(ps.Id);
            var analyzer = new TriggerOnMemoryUsage(ps.Id);
            analyzer.Start(OnTrigger);

            Console.ReadKey();

            analyzer.Dispose();
        }

        private void OnTrigger(TraceEvent traceEvent)
        {
            if (traceEvent.EventName == "GC/AllocationTick")
            {
                var obj = traceEvent as GCAllocationTickTraceData;
                Console.WriteLine($"{obj.ClrInstanceID} - {obj.TypeName} - {obj.AllocationAmount} - {obj.AllocationKind}");
            }
            else
            {
                var payload = traceEvent.GetPayload();
                if (payload == null) return;
                var name = payload["Name"]?.ToString();
                if (name == "working-set")
                {
                    var mean = (double)payload["Mean"];
                    var units = payload["DisplayUnits"].ToString();
                    Console.WriteLine($"Working-set: {mean}{units}");
                }
            }

        }
    }
}
