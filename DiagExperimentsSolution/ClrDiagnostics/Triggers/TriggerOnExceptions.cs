using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Text;

using ClrDiagnostics.Extensions;

using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;

namespace ClrDiagnostics.Triggers
{
    public class TriggerOnExceptions : TriggerBase
    {
        public TriggerOnExceptions(int processId) : base(processId)
        {
            this.AddProvider("Microsoft-Windows-DotNETRuntime",
                EventLevel.Verbose, -1);

            this.AddProvider("Microsoft-DotNETCore-SampleProfiler",
                EventLevel.Verbose, (long)ClrTraceEventParser.Keywords.All);
        }

        protected override void OnSubscribe(EventPipeEventSource source)
        {
            source.Clr.ExceptionStart += traceEvent => Trigger(traceEvent);
        }

    }
}
