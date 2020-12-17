using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Text;
using System.Threading;

// There are two ways to consume the events produced by this eventsource:
// 1. Using DynamicTraceEventParser 
// https://github.com/microsoft/perfview/blob/master/documentation/TraceEvent/TraceEventProgrammersGuide.md#static-vs-dynamic-traceeventparser-parsers
// 2. By generating a staticly typed parser. This can be done with the traceparsergen utility
// traceparsergen.exe <path to dll> -EventSource:Microsoft-System-Net-Http
// TraceParserGen /?
// more info here: https://github.com/Microsoft/perfview/blob/master/documentation/TraceEvent/TraceEventProgrammersGuide.md#building-compile-time-traceeventparser-parsers-using-traceparsergen
// 
// traceparsergen sources are here:
// https://github.com/Microsoft/perfview/tree/master/src/TraceParserGen
// build running: msbuild
//
// https://github.com/microsoft/perfview/issues/1157
//
// System.Diagnostics.Tracing (facade):
// https://github.com/dotnet/runtime/tree/master/src/libraries/System.Diagnostics.Tracing
//
// System.Diagnostics.Tracing (implementation):
// https://github.com/dotnet/runtime/blob/master/src/libraries/System.Private.CoreLib/src/System/Diagnostics/Tracing

namespace CustomEventSource
{
    public static class Constants
    {
        public const string CustomHeaderEventSourceName = "Raf-CustomHeader";
        public const string TriggerHeaderName = "X-TriggerHeaderEventSource";
        public const string TriggerHeaderCounterName = "TriggerHeader";
    }

    [EventSource(Name = Constants.CustomHeaderEventSourceName)]
    public class CustomHeaderEventSource : EventSource
    {
        private long _triggerHeaderCounter;
        public static readonly CustomHeaderEventSource Instance =
            new CustomHeaderEventSource();

        private CustomHeaderEventSource() :
            base(Constants.CustomHeaderEventSourceName
                , EventSourceSettings.EtwSelfDescribingEventFormat)
        {
        }

        public EventCounter TriggerHeader { get; private set; }

        public void RaiseTriggerHeaderCounter()
        {
            var nextValue = Interlocked.Increment(ref _triggerHeaderCounter);
            TriggerHeader.WriteMetric(nextValue);
        }

        protected override void OnEventCommand(EventCommandEventArgs command)
        {
            if (command.Command == EventCommand.Enable)
            {
                TriggerHeader = new EventCounter(Constants.TriggerHeaderCounterName, this)
                {
                    DisplayName = "Count of the custom header received on any request",
                    DisplayUnits = "Num",
                };
            }

            //base.OnEventCommand(command);
        }
    }
}
