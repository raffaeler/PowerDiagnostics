using System;
using System.Diagnostics;
using System.Linq;

// https://github.com/microsoft/clrmd/commits/master
// MiniDumpReader
// ClrObject.IsBoxedValue
// IDataReader.SearchMemory => MemorySearcher static helper

// https://github.com/dotnet/diagnostics/blob/master/documentation/design-docs/diagnostics-client-library.md

namespace TestConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            var ps = GetProcess("StaticMemoryLeaks");

            //var s = new UseCase1();
            //var s = new UseCase2();
            var s = new UseCase3();

            s.Analyze(ps.Id);

        }

        private static Process GetProcess(string processName)
        {
            var pss = Process.GetProcessesByName(processName);
            if (pss.Length != 1)
            {
                Console.WriteLine($"Please run a single target process called '{processName}'");
                return null;
            }

            return pss.Single();
        }
    }
}
