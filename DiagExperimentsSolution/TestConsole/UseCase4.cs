using System;
using System.Collections.Generic;
using System.Text;

using TestConsole.Triggers;

namespace TestConsole
{
    public class UseCase4
    {
        public void Analyze(int pid = -1)
        {
            //var analyzer = new TriggerOnCpuLoad(pid);
            var analyzer = new TriggerOnMemoryUsage(pid);
            analyzer.Start();

            Console.ReadKey();

            analyzer.Dispose();
        }
    }
}
