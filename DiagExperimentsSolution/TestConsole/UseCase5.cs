using System;
using System.Collections.Generic;
using System.Text;

using TestConsole.Triggers;

namespace TestConsole
{
    class UseCase5
    {
        public void Analyze(int pid = -1)
        {
            //var analyzer = new TriggerOnCpuLoad(pid);
            var analyzer = new TriggerOnExceptions(pid);
            analyzer.Start();

            Console.ReadKey();

            analyzer.Dispose();
        }

    }
}
