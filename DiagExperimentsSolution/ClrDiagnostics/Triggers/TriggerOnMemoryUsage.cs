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
        }

        protected override void OnSubscribe(EventPipeEventSource source)
        {
            source.Clr.GCAllocationTick += _ => Trigger();
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
