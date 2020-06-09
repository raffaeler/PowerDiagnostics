using ClrDiagnostics.Helpers;
using ClrDiagnostics.Triggers;

using System;
using System.Collections.Generic;
using System.Text;

using TestConsole.Helpers;
using TestConsole.Triggers;

namespace TestConsole
{
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
            analyzer.Start();

            Console.ReadKey();

            analyzer.Dispose();
        }

    }
}
