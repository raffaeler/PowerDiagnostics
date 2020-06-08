using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

using TestConsole.Helpers;
using TestConsole.LinqToDump;

namespace TestConsole
{
    public class UseCase3
    {
        private static string _dumpDir = @"H:\dev.git\Experiments\NetCoreExperiments\DiagnosticHelpers\_dumps";
        private static string _dumpName = "jsonnet.dmp";

        public void Analyze()
        {
            DumpSource analyzer;
            var ps = ProcessHelper.GetProcess("TestAllocation");
            if (ps == null)
            {
                var fullDumpName = Path.Combine(_dumpDir, _dumpName);
                analyzer = new DumpSource(fullDumpName);
            }
            else
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();
                analyzer = new DumpSource(ps.Id);
                var elapsed = sw.Elapsed;
                Console.WriteLine($"Process snapshot took {sw.ElapsedMilliseconds}ms");
            }

            using (analyzer)
            {
                var staticFields = analyzer.GetStaticFields().ToList();
                var staticFieldsWithGraphSize = analyzer.GetStaticFieldsWithGraphSize().ToList();
                var staticFieldsWithGraphAndSize = analyzer.GetStaticFieldsWithGraphAndSize().ToList();
                var dups = analyzer.GetDuplicateStrings();
                var large = analyzer.GetObjectsBySize();
                var largeStrings = analyzer.GetStringsBySize(256);
                var dumpHeapStat = analyzer.DumpHeapStat(0);
                // the following object arrays hold the statics and are counted in SOS "DumpHeap -stat"
                // The pinned objects are the missing ones, the other two are already in the previous list
                var objArray = analyzer.Roots.Where(r => r.Object.Type.Name == "System.Object[]").ToList();
            }
        }
    }
}
