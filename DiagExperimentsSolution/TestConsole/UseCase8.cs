using ClrDiagnostics;

using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Interfaces;
using Microsoft.Diagnostics.Tracing.Analysis.GC;
using Microsoft.Diagnostics.Tracing.AutomatedAnalysis;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

using static Microsoft.Diagnostics.Runtime.GCRoot;

namespace TestConsole;

public class UseCase8
{
    private const string _dumpFile = @"H:\_dumps\dump_20260605_202822.dmp";

    public void Analyze()
    {
        using var analyzer = DiagnosticAnalyzer.FromDump(_dumpFile, true);

        var memPressureService = analyzer.Objects
            .Where(o => o.Type?.Name == "TestWebApp.Services.MemoryPressureService")
            .Single();

        var graphRootsLists = analyzer.Objects
            .Where(o => o.Type?.Name == "System.Collections.Generic.List<TestWebApp.Services.GraphRoot>")
            .ToList();

        var graphRootsArrs = analyzer.Objects
            .Where(o => o.Type?.Name == "TestWebApp.Services.GraphRoot[]")
            .ToList();

        var graphRoots = analyzer.Objects
            .Where(o => o.Type?.Name == "TestWebApp.Services.GraphRoot")
            .ToList();

        var childLists = analyzer.Objects
            .Where(o => o.Type?.Name == "System.Collections.Generic.List<TestWebApp.Services.Child1>")
            .ToList();

        var childArrs = analyzer.Objects
            .Where(o => o.Type?.Name == "TestWebApp.Services.Child1[]")
            .ToList();

        var child1s = analyzer.Objects
            .Where(o => o.Type?.Name == "TestWebApp.Services.Child1")
            .ToList();

        var grandChild1Lists = analyzer.Objects
            .Where(o => o.Type?.Name == "System.Collections.Generic.List<TestWebApp.Services.GrandChild1>")
            .ToList();

        var grandChild1Arrs = analyzer.Objects
            .Where(o => o.Type?.Name == "TestWebApp.Services.GrandChild1[]")
            .ToList();

        var grandChild1s = analyzer.Objects
            .Where(o => o.Type?.Name == "TestWebApp.Services.GrandChild1")
            .ToList();

        var staticFieldRoot = analyzer.GetStaticFields()
            .Where(s => s.field.Name == "_roots")
            .Single();

        var staticFieldArray = analyzer.GetStaticFields()
            .Where(s => s.field.Name == "_arrays")
            .Single();

        var memPressureServiceFromRoots = analyzer.ObjectsWithStaticFields
            .Where(x => x.Item2.Name == "_roots")
            .Single();

        var graphRootList = graphRootsLists[0];
        var graphRootArr = graphRootsArrs[1];
        var graphRoot = graphRoots[7];
        var childList = childLists[1];
        var childArr = childArrs[2];
        var child1 = child1s[2];
        var grandChildList = grandChild1Lists[2];
        var grandChildArr = grandChild1Arrs[2];
        var grandChild1 = grandChild1s[4];
        Console.WriteLine($"  {graphRootList.Address:X16}  List<GraphRoot>");
        Console.WriteLine($"  {graphRootArr.Address:X16}  GraphRoot[]");
        Console.WriteLine($"  {graphRoot.Address:X16}  GraphRoot");
        Console.WriteLine($"  {childList.Address:X16}  List<Child>");
        Console.WriteLine($"  {childArr.Address:X16}  Child[]");
        Console.WriteLine($"  {child1.Address:X16}  child1");
        Console.WriteLine($"  {grandChildList.Address:X16}  List<GrandChild>");
        Console.WriteLine($"  {grandChildArr.Address:X16}  GrandChild[]");
        Console.WriteLine($"  {grandChild1.Address:X16}  grandChild1");

        var roots = analyzer.Roots.ToList();

        var grandToChild = PathExists(analyzer.Heap, grandChild1, child1);
        var childToRoot = PathExists(analyzer.Heap, child1, staticFieldRoot.obj);
        var grandToRoot = PathExists(analyzer.Heap, grandChild1, staticFieldRoot.obj);
        //var childToMemSvc = PathExists(analyzer.Heap, child1, memPressureService); //false
        PrintChain(analyzer.Heap, grandChild1, staticFieldRoot.obj);

        var myR = analyzer.Roots
            .Where(r => r.Object.Address == 0x0000021C7BD155E8)
            .ToList();  // zero

        Console.WriteLine($"MemoryPressureService: {memPressureService}");
        Console.WriteLine($"MemoryPressureService from Roots: {memPressureServiceFromRoots.Item1.Type?.Name}");
        Console.WriteLine($"GraphRoots: {graphRoots.Count}");
        Console.WriteLine($"Child1s: {child1s.Count}");
        Console.WriteLine($"GrandChild1s: {grandChild1s.Count}");
        Console.WriteLine($"StaticFieldRoot: {staticFieldRoot.field.Name} {staticFieldRoot.obj.Address.ToString("X16")} {staticFieldRoot.obj.Type?.Name}");
        Console.WriteLine($"StaticFieldArray: {staticFieldArray.field.Name} {staticFieldArray.obj.Address.ToString("X16")} {staticFieldArray.obj.Type?.Name}");

        var sampleGrandChild = grandChild1s[4]; // the fifth grandchild
        ulong addr = sampleGrandChild;
        ulong sampleAddress = sampleGrandChild.Address;
        Debug.Assert(addr == sampleAddress);

        Console.WriteLine();
        Console.WriteLine("===== Path to static Fields: =====");

        var pathsToStatics = analyzer.FindPathsToStatics(sampleGrandChild);
        foreach (var path in pathsToStatics)
        {
            PrintChain(analyzer.Heap, path.Item2);
        }

        Console.WriteLine();
        Console.WriteLine();

        //Console.WriteLine("===== Static Fields: =====");
        //PrintStaticFields(analyzer);
        //Console.WriteLine();

        Console.WriteLine("===== References: =====");
        PrintObjectReferences(analyzer, sampleGrandChild);
        Console.WriteLine();

        Console.WriteLine("===== References with Fields: =====");
        PrintObjectReferencesWithFields(analyzer, sampleGrandChild);
        Console.WriteLine();

        Console.WriteLine("===== EnumPath: =====");
        EnumPathv3(analyzer.Heap, sampleGrandChild);
        Console.WriteLine();

        Console.WriteLine("===== Root Paths: =====");
        PrintRootPahts(analyzer, sampleGrandChild);
        Console.WriteLine();

        var almost = analyzer.Objects.Single(o => o.Address == 0x0000021C7BD21BE0);
        // forward references:
        var reffwd = analyzer.ObjectReferencesWithFields(almost);

        //var refback = analyzer.FindReferencing(true, almost.Address);

        var collection = analyzer.ObjectsWithInstanceFields
            .Single(x => x.Item1.Type?.Name?.StartsWith("System.Collections.Generic.List<TestWebApp.Services.GraphRoot>") ?? false);

        var x = analyzer.GetStaticFields();
        var list = analyzer.ObjectReferencesWithFields(staticFieldRoot.obj).ToList();
        var arrInList = list[0];
        var unk = analyzer.ObjectReferencesWithFields(arrInList.Object).ToList();

        var res = EnumerateRootPathsRaf(analyzer.Heap, sampleGrandChild).ToList();

        var myRoots = //roots
            analyzer.GetStaticFields().Select(f => (Field: f.field, Object: f.obj))
            .ToList();

        Console.WriteLine(":::: Alternate");
        foreach (var root in myRoots)
        {
            //GCRoot gcRoot = new(analyzer.Heap, [root.Object.Address]);
            //var path = gcRoot.FindPathFrom(sampleGrandChild);

            GCRoot gcRoot = new(analyzer.Heap, [sampleGrandChild.Address]);
            var path = gcRoot.FindPathFrom(root.Object);

            PrintChain(analyzer.Heap, path);
        }

        Console.WriteLine(":::: EndAlternate");


        foreach (var root in myRoots)
        {
            if (root.Object.IsArray)
            {
                var array = root.Object.AsArray();
                var len = array.GetLength(0);
                for (int i = 0; i < len; i++)
                {
                    try
                    {
                        var item = array.GetObjectValue(i);
                        if (item.Type?.Name == "System.Collections.Generic.List<TestWebApp.Services.GraphRoot>")
                        {
                            Console.WriteLine("Found");
                        }

                        if (item.Address == 0x0000021C7BD155E8)
                        {
                            Console.WriteLine("FoundA");
                        }

                        var children = analyzer.ObjectReferencesWithFields(item).ToList();
                        foreach (var child in children)
                        {
                            if (child.Object.Type?.Name == "System.Collections.Generic.List<TestWebApp.Services.GraphRoot>")
                            {
                                Console.WriteLine("Found");
                            }

                            if (child.Object.Address == 0x0000021C7BD155E8)
                            {
                                Console.WriteLine("FoundA");
                            }
                        }
                    }
                    catch (Exception) { }
                }
            }
            else
            {
                if (root.Object.Type?.Name == "System.Collections.Generic.List<TestWebApp.Services.GraphRoot>")
                {
                    Console.WriteLine("Found");
                }

                if (root.Object.Address == 0x0000021C7BD155E8)
                {
                    Console.WriteLine("FoundA");
                }


                var children = analyzer.ObjectReferencesWithFields(root.Object).ToList();
                foreach (var child in children)
                {
                    if (child.Object.Type?.Name == "System.Collections.Generic.List<TestWebApp.Services.GraphRoot>")
                    {
                        Console.WriteLine("Found");
                    }

                    if (child.Object.Address == 0x0000021C7BD155E8)
                    {
                        Console.WriteLine("FoundA");
                    }
                }
            }
            //var children = analyzer.ObjectReferencesWithFields(root.Object).ToList();
            //var bingo = children
            //    .Where(c => c.)
            //    .ToList();
        }

    }

    private void PrintStaticFields(DiagnosticAnalyzer analyzer)
    {
        var staticFields = analyzer.GetStaticFields();
        foreach (var staticField in staticFields)
        {
            var field = staticField.field;
            var obj = staticField.obj;
            Console.WriteLine($"Type:{field?.Type?.Name} Field:{field?.Name} Obj:0x{obj:X16}");

            //var size = staticField.Size;
            //Console.WriteLine($"Type:{field?.Type?.Name} Field:{field?.Name} Obj:0x{obj:X16} Size:{size}");
        }
    }

    private void PrintObjectReferences(DiagnosticAnalyzer analyzer,
        ClrObject obj, int indent = 0)
    {
        var refs = analyzer.ObjectReferences(obj);
        foreach (var reference in refs)
        {
            var type = analyzer.GetObjectType(reference);
            Console.WriteLine($"{new string(' ', indent * 2)}0x{reference:X16} {type?.Name ?? "?"}");
            //PrintObjectReferences(analyzer, reference, indent + 1);
        }
    }

    private void PrintObjectReferencesWithFields(DiagnosticAnalyzer analyzer,
        ClrObject obj, int indent = 0)
    {
        var refs = analyzer.ObjectReferencesWithFields(obj);
        foreach (var reference in refs)
        {
            var type = analyzer.GetObjectType(reference.Object);
            Console.WriteLine($"{new string(' ', indent * 2)}0x{reference.Object.Address:X16} {type?.Name ?? "?"} field:{reference.Field?.Name ?? ""}");
            //PrintObjectReferencesWithFields(analyzer, reference.Object, indent + 1);
        }
    }

    private void PrintRootPahts(DiagnosticAnalyzer analyzer, ClrObject obj)
    {
        var rootPaths = analyzer.GetRootPaths(obj);
        foreach (var rootPath in rootPaths)
        {
            var root = rootPath.Root;
            var path = rootPath.Path;
            string label;
            if (root != null)
            {
                var rootKindLabel = root.Address == 0
                    ? "Register"
                    : root.RootKind.ToString();
                label = $"Root {rootKindLabel} Addr:{root.Address} {root.Object.Type?.Name}";
            }
            else
            {
                label = rootPath.StaticField != null
                ? $"Static {rootPath.StaticField.Type?.Name ?? "?"}.{rootPath.StaticField.Name ?? "?"}"
                : "StaticField";
                var firstLink = rootPath.Path;
                var addr = firstLink != null
                    ? $"0x{firstLink.Object:X16}"
                    : "0x????????????????";
                var typeName = firstLink != null
                    ? analyzer.Heap.GetObjectType(firstLink.Object)?.Name ?? "?"
                    : "?";

                label += $" Addr:{addr} {typeName} {typeName}";
            }
            Console.WriteLine(label);

            for (GCRoot.ChainLink? link = path; link != null; link = link.Next)
            {
                var address = link.Object;
                var type = analyzer.GetObjectType(address);
                Console.WriteLine($"     {address:X16} {type?.Name ?? "?"}");
            }
        }
    }

    private void EnumPathv3(ClrHeap heap, ClrObject obj)
    {
        var targetAddress = obj.Address;

        GCRoot gcroot = new(heap, [targetAddress]);

        foreach ((ClrRoot root, GCRoot.ChainLink path) in gcroot.EnumerateRootPaths())
        {
            Console.Write($"{root} -> ");

            // Walk the chain of objects from the root to the target.
            GCRoot.ChainLink? link = path;
            while (link is not null)
            {
                Console.Write($"{link.Object:x}");
                link = link.Next;
                if (link is not null)
                    Console.Write(" -> ");
            }

            Console.WriteLine();
        }
    }

    public IEnumerable<(ClrRoot Root, ChainLink Path)> EnumerateRootPathsRaf(
        ClrHeap heap, ClrObject obj,
        CancellationToken cancellation = default)
    {
        var targetAddress = obj.Address;
        GCRoot gcroot = new(heap, [targetAddress]);


        IEnumerable<ClrRoot> roots = heap.EnumerateRoots();
        foreach (ClrRoot root in roots)
        {
            cancellation.ThrowIfCancellationRequested();
            ChainLink? path = gcroot.FindPathFrom(root.Object, cancellation);
            if (path is not null)
                yield return (root, path);
        }
    }

    //public ChainLink? FindPathFrom(ClrObject start)
    //{
    //    return FindPathFrom(start, CancellationToken.None);
    //}

    //private Dictionary<ulong, ChainLink> _found = new();
    //private Predicate<ClrObject>? _targetPredicate;
    //public ChainLink? FindPathFrom(ClrObject start, CancellationToken cancellation)
    //{
    //    if (_found.TryGetValue(start, out ChainLink? link))
    //        return link;

    //    if (_targetPredicate is not null && _targetPredicate(start))
    //    {
    //        link = new ChainLink()
    //        {
    //            Object = start,
    //        };

    //        _found.Add(start, link);
    //        return link;
    //    }

    //    if (start.Type is null || !start.Type.ContainsPointers)
    //        return null;

    //    List<byte[]> stack = new();
    //    link = WalkObject(stack, 0, start, cancellation);
    //    if (link is not null)
    //    {
    //        DrainUnwalkedFromSeen(stack);
    //        return link;
    //    }

    //    while (stack.Count > 0)
    //    {
    //        cancellation.ThrowIfCancellationRequested();

    //        ReferenceList curr = stack[stack.Count - 1];
    //        ulong currChild = curr.Next();
    //        if (currChild == 0)
    //        {
    //            stack.RemoveAt(stack.Count - 1);
    //            curr.Dispose();
    //            continue;
    //        }

    //        TraceConsidering(curr.Object, currChild);

    //        link = WalkObject(stack, curr.Object, currChild, cancellation);
    //        if (link is not null)
    //            return CleanupAndGetResult(stack, link, curr);
    //    }

    //    return null;
    //}

    private bool PathExists(ClrHeap heap, ClrObject from, ClrObject to)
    {
        GCRoot gcroot = new(heap, [from.Address]);
        return PathExists(gcroot, to);
    }

    private bool PathExists(GCRoot from, ClrObject to)
    {
        ChainLink? link = from.FindPathFrom(to);
        if (link != null) return true;
        return false;
    }

    private void PrintChain(ClrHeap heap, ClrObject from, ClrObject to)
    {
        GCRoot gcroot = new(heap, [from.Address]);
        ChainLink? link = gcroot.FindPathFrom(to);
        for (GCRoot.ChainLink? l = link; l != null; l = l.Next)
        {
            var type = heap.GetObjectType(l.Object);
            Console.WriteLine($"     {l.Object:X16} {type?.Name ?? "?"}");
        }
    }

    private void PrintChain(ClrHeap heap, ChainLink? link)
    {
        for (GCRoot.ChainLink? l = link; l != null; l = l.Next)
        {
            var type = heap.GetObjectType(l.Object);
            Console.WriteLine($"     {l.Object:X16} {type?.Name ?? "?"}");
        }
    }




}
