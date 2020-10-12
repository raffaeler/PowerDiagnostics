using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using ClrDiagnostics.Helpers;
using ClrDiagnostics.Triggers;
using Microsoft.Diagnostics.Tracing;
using ClrDiagnostics.Extensions;

namespace TestConsole
{
    /// <summary>
    /// Used to test triggers.
    /// These are the perf counters that can be used to trigger a snapshot or dump
    /// </summary>
    class UseCase6
    {
        public void Analyze()
        {
            var ps = ProcessHelper.GetProcess("TestWebApp");
            if (ps == null)
            {
                Console.WriteLine("Run the required process first");
                return;
            }

            var analyzer = new TriggerOnHttpRequests(ps.Id, 900);
            analyzer.Start(OnTrigger);

            Console.ReadKey();

            analyzer.Dispose();
        }

        private void OnTrigger(TraceEvent traceEvent)
        {
            var payload = traceEvent.GetPayload();
            string counterName = (string)payload["Name"];
            if (counterName == "requests-per-second")
            {
                var increment = (double)payload["Increment"];
                if (increment > 0)
                {
                    Console.WriteLine($"{counterName} - {increment}");
                }
            }
            //if (counterName == "current-requests")
            //{
            //    var count = (int)payload["Count"];
            //    var mean = (double)payload["Mean"];
            //    if (count > 0)
            //    {
            //        Console.WriteLine($"{counterName} - {count} - {mean}");
            //    }
            //}
        }

    }
}
