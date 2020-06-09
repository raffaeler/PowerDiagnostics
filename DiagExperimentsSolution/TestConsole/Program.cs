using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using TestConsole.Helpers;
using TestConsole.Triggers;

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
            //var s = new UseCase1();
            //var s = new UseCase2();
            //var s = new UseCase3();
            //var s = new UseCase4();
            var s = new UseCase5();
            //var s = new UseCase6();
            s.Analyze();
        }
    }
}
