using ClrDiagnostics;

using Microsoft.Diagnostics.Runtime;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace TestConsole;
/// <summary>
/// Diagnosing problems related to LoadeAllocator
/// </summary>
public class UseCase2
{
    private static string _dumpDir = @"H:\dev.git\Experiments\NetCoreExperiments\DiagnosticHelpers\_dumps";
    private static string _dumpName = "jsonnet.dmp";

    public void Analyze()
    {
        var fullDumpName = Path.Combine(_dumpDir, _dumpName);
        var analyzer = DiagnosticAnalyzer.FromDump(fullDumpName, true);

        var collectibles = analyzer.Objects
            .Where(o => o.Type!.IsCollectible).ToList();
        var allocator = analyzer.GetObjectsGroupedByAllocator(analyzer.Objects);

        //var objs = analyzer.GetObjectsOfType("System.Reflection.LoaderAllocator", true);
        var objs = analyzer.Objects
            .Where(o => o.Type!.Name == "System.Reflection.LoaderAllocator");
        foreach (var leakedObj in objs)
        {
            var leakedType = leakedObj.Type!;
            Console.WriteLine($"{leakedType.Name} Addr:0x{leakedObj.Address:X} Size:{leakedObj.Size} MT:0x{leakedType.MethodTable:X}");

            var roots = analyzer.GetRootPaths(leakedObj);
            int i = 0;
            foreach (var tplRoot in roots)
            {
                string rootKindLabel;
                string rootAddr;
                string rootTypeName;
                string rootAlloc;

                if (tplRoot.Root != null)
                {
                    rootKindLabel = tplRoot.Root.Address == 0
                        ? "Register"
                        : tplRoot.Root.RootKind.ToString();
                    rootAddr = $"0x{tplRoot.Root.Address:X}";
                    rootTypeName = tplRoot.Root.Object.Type?.Name ?? "?";
                    rootAlloc = $"alloc:{tplRoot.Root.Object.Type?.LoaderAllocatorHandle:X16}";
                }
                else
                {
                    rootKindLabel = tplRoot.StaticField != null
                        ? $"Static {tplRoot.StaticField.Type?.Name ?? "?"}.{tplRoot.StaticField.Name ?? "?"}"
                        : "StaticField";
                    var firstLink = tplRoot.Path;
                    rootAddr = firstLink != null
                        ? $"0x{firstLink.Object:X}"
                        : "?";
                    rootTypeName = firstLink != null
                        ? analyzer.GetObjectType(firstLink.Object)?.Name ?? "?"
                        : "?";
                    rootAlloc = "alloc:?";
                }

                Console.WriteLine($"Root {rootKindLabel} Addr:{rootAddr} {rootTypeName} Addr:{rootAddr}  {rootAlloc}");

                // new in v3
                var path = tplRoot.Path;

                Console.WriteLine($"  Path {i++}");
                //foreach (var path in tplRoot.Path)
                for (GCRoot.ChainLink? link = path; link != null; link = link.Next)
                {
                    var address = link.Object;
                    var type = analyzer.GetObjectType(address);

                    Console.WriteLine($"     {address:X16} {type?.Name ?? "?"} alloc:{leakedType.LoaderAllocatorHandle:X16}");
                    var result = FindReferencing(analyzer, false, address);
                    if (result.Count > 0)
                    {
                        Console.WriteLine($"                 Objects whose fields point to {address:X16}");
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
                foreach (var instanceField in obj.Type!.Fields.Where(f => f.IsObjectReference))
                {
                    // returns zero if it is not an ulong
                    var staticFieldAddress = instanceField.Read<ulong>(obj.Address, false);
                    if (leakedAddresses.Contains(staticFieldAddress))
                    {
                        result.Add((obj.Address, obj.Type!.Name!, instanceField.Name!, false));
                    }
                }
            }

            foreach (var staticField in obj.Type!.StaticFields.Where(f => f.IsObjectReference))
            {
                // returns zero if it is not an ulong
                var staticFieldAddress = staticField.Read<ulong>(mainAppDomain);
                if (leakedAddresses.Contains(staticFieldAddress))
                {
                    result.Add((obj.Address, obj.Type!.Name!, staticField.Name!, true));
                }
            }
        }

        return result;
    }


    private static void Search(DiagnosticAnalyzer analyzer, ulong leakedAddress)
    {
        var leakedObj = analyzer.Heap.GetObject(leakedAddress);
        var roots = analyzer.GetRootPaths(leakedObj);

        // get the statics being kept by some static field
        // v3: GCRoot.ChainLink is a linked list, walk manually instead of LINQ Skip/Take
        var addressesToSearch = roots
            .Where(r => r.Root?.IsPinned == true && r.Root.Object.Type?.Name == "System.Object[]")
            .Select(r => r.Path?.Next)
            .Where(link => link != null)
            .Select(link => link!.Object)
            .ToList();


        var all = analyzer.Objects;
        var mainAppDomain = analyzer.MainAppDomain;

        foreach (var obj in all)
        {
            foreach (var staticField in obj.Type!.StaticFields)
            {
                var staticFieldAddress = staticField.Read<ulong>(mainAppDomain);
                if (addressesToSearch.Contains(staticFieldAddress))
                {
                    Console.WriteLine($"{obj.Address:16X} {obj.Type!.Name}");
                    Console.WriteLine($"  Field: {staticField.Name}");
                }
            }
        }
    }

}

