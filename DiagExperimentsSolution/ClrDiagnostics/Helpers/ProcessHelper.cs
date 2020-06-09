using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace ClrDiagnostics.Helpers
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

        public static Process GetOrStartProcess(string processName, string filename)
        {
            var pss = Process.GetProcessesByName(processName);
            if (pss.Length == 0)
            {
                return Process.Start(filename);
            }

            if (pss.Length == 1)
            {
                return pss.Single();
            }

            return null;
        }

    }
}
