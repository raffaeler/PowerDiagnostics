using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

using ClrDiagnostics;
using ClrDiagnostics.Helpers;

namespace TestConsole
{
    public class UseCase3
    {
        private static string _dumpDir = @"H:\dev.git\Experiments\NetCoreExperiments\DiagnosticHelpers\_dumps";
        private static string _dumpName = "graphdump.dmp";
        //private static string _dumpName = "jsonnet.dmp";
        //private static string _pdbName = "NetCore3.pdb";

        public void Analyze()
        {
            DiagnosticAnalyzer analyzer;
            var ps = ProcessHelper.GetProcess("TestWebApp");
            if (ps == null)
            {
                var fullDumpName = Path.Combine(_dumpDir, _dumpName);
                //var fullPdbName = Path.Combine(_dumpDir, _pdbName);
                //analyzer = DiagnosticAnalyzer.FromDump(fullDumpName, true, fullPdbName);

                analyzer = DiagnosticAnalyzer.FromDump(fullDumpName, true);
            }
            else
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();
                analyzer = DiagnosticAnalyzer.FromSnapshot(ps.Id);
                var elapsed = sw.Elapsed;
                Console.WriteLine($"Process snapshot took {sw.ElapsedMilliseconds}ms");
            }

            using (analyzer)
            {
                analyzer.PrintClrStack();
                analyzer.PrintDumpHeapStat(0);

                var testObjects = analyzer.Objects
                    .Where(o => o.Type.Name == null || (!o.Type.IsFree && !o.Type.Name.Contains("System"))).ToList();
                var mine = analyzer.Objects.Where(o => o.Type.MethodTable == 0x00007ffefd118688).FirstOrDefault();

                var jsonConvert = analyzer.Objects
                    .Where(o => o.Type.Name != null && o.Type.Name.Equals("Newtonsoft.Json.Serialization.JsonProperty"))
                    .FirstOrDefault();

                var byAllocatorAddress = analyzer.GetObjectsGroupedByAllocator(analyzer.Objects).ToList();
                foreach (var item in byAllocatorAddress)
                {
                    var allocatorName = analyzer.GetAllocatorName(item.allocator);
                }

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


                //foreach(var str in analyzer.GetStringsBySize(80, 100))
                //{
                //    var s1 = str.Item1.AsString();
                //    var s2 = str.Item2;
                //    Console.WriteLine(s1);
                //    Console.WriteLine(s2);
                //}
            }
        }
    }
}
