using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using TestConsole.Triggers;
using CustomEventSource;

namespace TestConsole
{
    class UseCase6
    {
        public void Analyze(int pid = -1)
        {
            var analyzer = new TriggerOnCustomHeader(pid);
            analyzer.Start();

            Console.ReadKey();

            analyzer.Dispose();
        }

    }
}
