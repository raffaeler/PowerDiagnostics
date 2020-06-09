using ClrDiagnostics.Helpers;
using ClrDiagnostics.Triggers;

using System;
using System.Collections.Generic;
using System.Text;

using TestConsole.Helpers;
using TestConsole.Triggers;

namespace TestConsole
{
    public class UseCase4
    {
        public void Analyze()
        {
            var ps = ProcessHelper.GetProcess("StaticMemoryLeaks");
            if(ps == null)
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

        private void OnTrigger()
        {
        }
    }
}
