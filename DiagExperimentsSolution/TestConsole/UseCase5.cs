using System;
using System.Collections.Generic;
using System.Text;

using ClrDiagnostics.Helpers;
using ClrDiagnostics.Triggers;

using Microsoft.Diagnostics.Tracing;

namespace TestConsole
{
    /// <summary>
    /// Used to test triggers.
    /// These are the perf counters that can be used to trigger a snapshot or dump
    /// </summary>
    class UseCase5
    {
        public void Analyze()
        {
            //var ps = ProcessHelper.GetProcess("TestAllocation");
            var ps = ProcessHelper.GetProcess("TestExceptions");
            if (ps == null)
            {
                Console.WriteLine("Run the required process first");
                return;
            }

            //var analyzer = new TriggerOnCpuLoad(ps.Id);
            var analyzer = new TriggerOnExceptions(ps.Id);
            analyzer.Start(OnTrigger);

            Console.ReadKey();

            analyzer.Dispose();
        }

        private void OnTrigger(TraceEvent traceEvent)
        {
        }
    }
}
