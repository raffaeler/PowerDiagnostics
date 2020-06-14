using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using ClrDiagnostics;

namespace TestConsole
{
    public class UseCase2
    {
        private static string _dumpDir = @"H:\dev.git\Experiments\NetCoreExperiments\DiagnosticHelpers\_dumps";
        private static string _dumpName = "jsonnet.dmp";

        public void Analyze()
        {
            var fullDumpName = Path.Combine(_dumpDir, _dumpName);
            var analyzer = DiagnosticAnalyzer.FromDump(fullDumpName, true);

            var collectibles = analyzer.Objects
                .Where(o => o.Type.IsCollectible).ToList();
            var allocator = analyzer.GetObjectsGroupedByAllocator(analyzer.Objects);

            //var objs = analyzer.GetObjectsOfType("System.Reflection.LoaderAllocator", true);
            var objs = analyzer.Objects
                .Where(o => o.Type.Name == "System.Reflection.LoaderAllocator");
            foreach (var leakedObj in objs)
            {
                var leakedType = leakedObj.Type;
                Console.WriteLine($"{leakedType.Name} Addr:0x{leakedObj.Address:X} Size:{leakedObj.Size} MT:0x{leakedType.MethodTable:X}");

                var roots = analyzer.RootPaths(leakedObj.Address);
                bool isFirst = true;
                int i = 0;
                foreach (var root in roots)
                {
                    if (isFirst)
                    {
                        Console.WriteLine($"Root {root.Root.RootKind} Addr:{root.Root.Address} {root.Root.Object.Type.Name} Addr:{root.Root.Address}  alloc:{root.Root.Object.Type.LoaderAllocatorHandle:X16}");
                        isFirst = false;
                    }

                    Console.WriteLine($"  Path {i++}");
                    foreach (var path in root.Path)
                    {
                        Console.WriteLine($"     {path.Address:X16} {path.Type.Name} alloc:{leakedType.LoaderAllocatorHandle:X16}");
                        var result = FindReferencing(analyzer, false, path.Address);
                        if (result.Count > 0)
                        {
                            Console.WriteLine($"                 Objects whose fields point to {path.Address:X16}");
                            foreach (var res in result)
                            {
                                string isStaticString = res.isStatic ? "static" : "instance";
                                Console.WriteLine($"                   {res.address:X16} Type:{res.typeName} field:{res.fieldName} {isStaticString}");
                            }
                        }

                    }

                    Console.WriteLine();
                }
            }

            analyzer.Dispose();
        }

        private static List<(ulong address, string typeName, string fieldName, bool isStatic)> FindReferencing(
            DiagnosticAnalyzer analyzer, bool includeInstance, params ulong[] leakedAddresses)
        {
            var result = new List<(ulong address, string typeName, string fieldName, bool isStatic)>();
            var all = analyzer.Objects;
            var mainAppDomain = analyzer.MainAppDomain;

            foreach (var obj in all)
            {
                if (includeInstance)
                {
                    foreach (var instanceField in obj.Type.Fields.Where(f => f.IsObjectReference))
                    {
                        // returns zero if it is not an ulong
                        var staticFieldAddress = instanceField.Read<ulong>(obj.Address, false);
                        if (leakedAddresses.Contains(staticFieldAddress))
                        {
                            result.Add((obj.Address, obj.Type.Name, instanceField.Name, false));
                        }
                    }
                }

                foreach (var staticField in obj.Type.StaticFields.Where(f => f.IsObjectReference))
                {
                    // returns zero if it is not an ulong
                    var staticFieldAddress = staticField.Read<ulong>(mainAppDomain);
                    if (leakedAddresses.Contains(staticFieldAddress))
                    {
                        result.Add((obj.Address, obj.Type.Name, staticField.Name, true));
                    }
                }
            }

            return result;
        }


        private static void Search(DiagnosticAnalyzer analyzer, ulong leakedAddress)
        {
            var roots = analyzer.RootPaths(leakedAddress);

            // get the statics being kept by some static field
            var staticRoots = roots
                .Where(r => r.Root.IsPinned && r.Root.Object.Type.Name == "System.Object[]")
                .SelectMany(r => r.Path.Skip(1).Take(1))
                .ToList();

            var addressesToSearch = staticRoots
                .Select(a => a.Address)
                .ToList();


            var all = analyzer.Objects;
            var mainAppDomain = analyzer.MainAppDomain;

            foreach (var obj in all)
            {
                foreach (var staticField in obj.Type.StaticFields)
                {
                    var staticFieldAddress = staticField.Read<ulong>(mainAppDomain);
                    if (addressesToSearch.Contains(staticFieldAddress))
                    {
                        Console.WriteLine($"{obj.Address:16X} {obj.Type.Name}");
                        Console.WriteLine($"  Field: {staticField.Name}");
                    }
                }
            }
        }

    }
}
