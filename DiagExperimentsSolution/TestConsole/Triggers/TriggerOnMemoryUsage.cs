using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Text;

using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;

namespace TestConsole.Triggers
{
    public class TriggerOnMemoryUsage : IDisposable
    {
        private DiagnosticsClient _client;
        private IList<EventPipeProvider> _providers = new List<EventPipeProvider>();
        private EventPipeSession _session;
        private EventPipeEventSource _source;

        public TriggerOnMemoryUsage(int processId)
        {
            _client = new DiagnosticsClient(processId);
            _providers.Add(new EventPipeProvider(
                "Microsoft-Windows-DotNETRuntime",
                EventLevel.Verbose,
                (long)-1));
            //(long)ClrTraceEventParser.Keywords.GC));
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
            _source.Clr.GCSuspendEEStart += Clr_GCSuspendEEStart;
            _source.Clr.GCSuspendEEStop += Clr_GCSuspendEEStop;
            _source.Clr.GCSampledObjectAllocation += Clr_GCSampledObjectAllocation;
            _source.Clr.GCAllocationTick += Clr_GCAllocationTick; ;
            _source.Clr.All += Clr_All;
            _source.Process();
        }

        private void Clr_GCSuspendEEStop(Microsoft.Diagnostics.Tracing.Parsers.Clr.GCNoUserDataTraceData obj)
        {
        }

        private void Clr_GCSuspendEEStart(Microsoft.Diagnostics.Tracing.Parsers.Clr.GCSuspendEETraceData obj)
        {
        }

        private void Clr_GCAllocationTick(
            Microsoft.Diagnostics.Tracing.Parsers.Clr.GCAllocationTickTraceData obj)
        {
            //obj.AllocationAmount64
            //obj.AllocationKind
            //obj.ClrInstanceID
            //obj.TypeID
            //obj.TypeName
            //obj.HeapIndex
            //obj.Address

            Console.WriteLine($"{obj.ClrInstanceID} - {obj.TypeName} - {obj.AllocationAmount} - {obj.AllocationKind}");
        }

        private void Clr_All(TraceEvent obj)
        {
            Console.WriteLine(obj.EventName);
        }

        private void Clr_GCSampledObjectAllocation(
            Microsoft.Diagnostics.Tracing.Parsers.Clr.GCSampledObjectAllocationTraceData obj)
        {
            Console.WriteLine($"Alloc: {obj.ClrInstanceID} {obj.Dump(true)}");
        }

        public void Stop()
        {
            //_source.StopProcessing();

            if (_source != null) { _source.Dispose(); _source = null; }
            if (_session != null) { _session.Dispose(); _session = null; }
        }
    }
}
