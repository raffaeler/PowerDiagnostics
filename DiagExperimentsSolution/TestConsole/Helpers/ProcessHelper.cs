using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace TestConsole.Helpers
{
    public static class ProcessHelper
    {
        public static Process GetProcess(string processName)
        {
            var pss = Process.GetProcessesByName(processName);
            if (pss.Length != 1)
            {
                return null;
            }

            return pss.Single();
        }

    }
}
