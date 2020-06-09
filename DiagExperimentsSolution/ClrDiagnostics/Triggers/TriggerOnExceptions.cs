using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Text;

using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;

namespace ClrDiagnostics.Triggers
{
    public class TriggerOnExceptions : IDisposable
    {
        private DiagnosticsClient _client;
        private IList<EventPipeProvider> _providers = new List<EventPipeProvider>();
        private EventPipeSession _session;
        private EventPipeEventSource _source;

        public TriggerOnExceptions(int processId)
        {
            _client = new DiagnosticsClient(processId);
            _providers.Add(
                new EventPipeProvider("Microsoft-Windows-DotNETRuntime",
                EventLevel.Verbose, (long)-1));
            _providers.Add(
                new EventPipeProvider("Microsoft-DotNETCore-SampleProfiler",
                EventLevel.Verbose, (long)ClrTraceEventParser.Keywords.All));
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
            _source.Clr.ExceptionStart += Clr_ExceptionStart;
            _source.Clr.ExceptionCatchStart += Clr_ExceptionCatchStart;
            _source.Clr.ExceptionFilterStart += Clr_ExceptionFilterStart;
            _source.Clr.ExceptionFinallyStart += Clr_ExceptionFinallyStart;

            _source.Clr.ExceptionFinallyStop += Clr_ExceptionFinallyStop;
            _source.Clr.ExceptionFilterStop += Clr_ExceptionFilterStop;
            _source.Clr.ExceptionCatchStop += Clr_ExceptionCatchStop;
            _source.Clr.ExceptionStop += Clr_ExceptionStop;

            //_source.Clr.All += Clr_All;
            _source.Process();
        }

        private void Clr_ExceptionStart(Microsoft.Diagnostics.Tracing.Parsers.Clr.ExceptionTraceData obj)
        {
            Console.WriteLine($"Start - Type: {obj.ExceptionType} Message:{obj.ExceptionMessage} TID:{obj.ThreadID} Level {obj.Level}");
        }

        private void Clr_ExceptionFilterStart(Microsoft.Diagnostics.Tracing.Parsers.Clr.ExceptionHandlingTraceData obj)
        {
            Console.WriteLine($"Filter / Start - Method:{obj.MethodName} TID:{obj.ThreadID} Level {obj.Level}");
        }

        private void Clr_ExceptionCatchStart(Microsoft.Diagnostics.Tracing.Parsers.Clr.ExceptionHandlingTraceData obj)
        {
            Console.WriteLine($"Catch / Start - Method:{obj.MethodName} TID:{obj.ThreadID} Level {obj.Level}");
        }

        private void Clr_ExceptionFinallyStart(Microsoft.Diagnostics.Tracing.Parsers.Clr.ExceptionHandlingTraceData obj)
        {
            Console.WriteLine($"Finally / Start - Method:{obj.MethodName} TID:{obj.ThreadID} Level {obj.Level}");
        }

        private void Clr_ExceptionFinallyStop(EmptyTraceData obj)
        {
            Console.WriteLine("Finally / Stop ");
        }

        private void Clr_ExceptionCatchStop(EmptyTraceData obj)
        {
            Console.WriteLine("Catch / Stop ");
        }

        private void Clr_ExceptionFilterStop(EmptyTraceData obj)
        {
            Console.WriteLine("Filter / Stop ");
        }

        private void Clr_ExceptionStop(EmptyTraceData obj)
        {
            Console.WriteLine("Stop ");
        }


        private void Clr_All(TraceEvent obj)
        {
            Console.WriteLine(obj.EventName);
        }

        public void Stop()
        {
            //_source.StopProcessing();

            if (_source != null) { _source.Dispose(); _source = null; }
            if (_session != null) { _session.Dispose(); _session = null; }
        }
    }
}
