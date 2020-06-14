using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Diagnostics.Tracing;
using CustomEventSource;
using ClrDiagnostics.Helpers;
using ClrDiagnostics.Triggers;
using ClrDiagnostics.Extensions;

namespace TestConsole
{
    class UseCase7
    {
        public void Analyze()
        {
            var ps = ProcessHelper.GetProcess("TestWebApp");
            if (ps == null)
            {
                Console.WriteLine("Run the required process first");
                return;
            }

            var analyzer = new TriggerOnEventCounter(ps.Id, Constants.CustomHeaderEventSourceName);
            analyzer.Start(
                OnTrigger,
                traceEvent => traceEvent.ProviderName.Equals(Constants.CustomHeaderEventSourceName));

            Console.ReadKey();

            analyzer.Dispose();
        }

        private void OnTrigger(TraceEvent traceEvent)
        {

        }

    }
}
