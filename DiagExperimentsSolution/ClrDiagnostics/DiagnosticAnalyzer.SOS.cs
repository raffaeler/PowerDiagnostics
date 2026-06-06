using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.Diagnostics.Runtime;
using ClrDiagnostics.Extensions;
using ClrDiagnostics.Models;
using DiagnosticModels;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;

namespace ClrDiagnostics;
/// <summary>
/// This source file contains the methods emulating the SOS commands
/// </summary>
public partial class DiagnosticAnalyzer
{
    /// <summary>
    /// equivalent to SOS dumpheap -stat
    /// </summary>
    public IEnumerable<(ClrType? type, List<ClrObject> objects, long size)> DumpHeapStat(
        long minTotalSize = 1024)
    {
        return Objects
            .GroupBy(o => o.Type, o => o)
            .Select(o => (type: o.Key, objects: o.ToList(), totalSize: o.Sum(s => (long)s.Size)))
            .Where(t => t.totalSize > minTotalSize)
            .OrderBy(t => t.totalSize)
            .ThenBy(t => t.type?.Name);
    }

    public void PrintDumpHeapStat(long minTotalSize = 1024)
    {
        var dumpHeapStat = DumpHeapStat(minTotalSize);
        var pinned = Roots.Where(r => r.IsPinned && r.Object.Type?.Name == "System.Object[]");

        var pinnedType = pinned.FirstOrDefault()?.Object.Type;
        var pinnedSize = pinned.Sum(p => (long)p.Object.Size);
        var pinnedCount = pinned.Count();

        Console.WriteLine("              MT    Count    TotalSize Class Name");
        foreach (var t in dumpHeapStat)
        {
            Console.WriteLine($"{t.type?.MethodTable:X16} {t.objects.Count,8} {t.size,12} {t.type?.Name}");
        }

        var total = dumpHeapStat.Sum(d => d.objects.Count);
        Console.WriteLine($"Total {total} objects");
        Console.WriteLine();
        Console.WriteLine("Roots:");
        if (pinnedType != null)
            Console.WriteLine($"{pinnedType.MethodTable:X16} {pinnedCount,8} {pinnedSize,12} {pinnedType.Name}");
    }

    public Task<string> PrintRootsAsync(ClrObject clrObject, Action<int> onProgress, CancellationToken cancellationToken)
    {
        return Task.Run<string>(() => PrintRoots(clrObject, onProgress, cancellationToken));
    }

    public int GetGraphPathsCount(ClrObject clrObject)
    {
        int count = 0;
        var objectType = clrObject.Type;
        var roots = GetRootPaths(clrObject);
        foreach (var root in roots)
        {
            // v3: GCRoot.ChainLink is a linked list; walk to count
            for (var link = root.Path; link != null; link = link.Next)
                count++;
        }

        return count;
    }

    /// <summary>
    /// Builds a structured GC root path tree directly from ClrMD data without string intermediates.
    /// </summary>
    /// <param name="clrObject">The object to analyze.</param>
    /// <param name="onProgress">Callback fired once per chain link processed.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <param name="maxPaths">Maximum number of root paths to enumerate.</param>
    /// <returns>A <see cref="GcRootPathResult"/> containing nested root nodes.</returns>
    public Task<GcRootPathResult> GetRootPathsAsync(
        ClrObject clrObject,
        Action<int> onProgress,
        CancellationToken cancellationToken,
        int maxPaths = -1)
    {
        return Task.Run(() =>
        {
            var result = new GcRootPathResult();

            // Use GetRootPaths to enumerate GCRoot paths + static field fallback
            IList<(ClrRoot? Root, ClrStaticField? StaticField, GCRoot.ChainLink Path)> allPaths;
            allPaths = GetRootPaths(clrObject);

            if (maxPaths > 0)
                allPaths = allPaths.Take(maxPaths).ToList();

            int progressCount = 0;

            foreach (var path in allPaths)
            {
                string rootKindLabel;
                string objectAddress;
                string typeName;

                if (path.Root != null)
                {
                    rootKindLabel = path.Root.Address == 0
                        ? "Register"
                        : path.Root.RootKind.ToString();
                    objectAddress = $"0x{path.Root.Address:X16}";
                    typeName = path.Root.Object.Type?.Name ?? "?";
                }
                else
                {
                    // Static field root: derive info from the StaticField and first chain link
                    rootKindLabel = path.StaticField != null
                        ? $"Static {path.StaticField.Type?.Name ?? "?"}.{path.StaticField.Name ?? "?"}"
                        : "StaticField";
                    var firstLink = path.Path;
                    objectAddress = firstLink != null
                        ? $"0x{firstLink.Object:X16}"
                        : "0x????????????????";
                    typeName = firstLink != null
                        ? _clrRuntime.Heap.GetObjectType(firstLink.Object)?.Name ?? "?"
                        : "?";
                }

                var rootNode = new GcRootPathNode
                {
                    ObjectAddress = objectAddress,
                    TypeName = typeName,
                    RootKind = rootKindLabel,
                    Depth = 0,
                };

                GcRootPathNode currentParent = rootNode;

                for (GCRoot.ChainLink? link = path.Path; link != null; link = link.Next)
                {
                    var address = link.Object;
                    var type = _clrRuntime.Heap.GetObjectType(address);

                    progressCount++;
                    if (cancellationToken.IsCancellationRequested)
                        throw new OperationCanceledException("Canceled by user request");

                    onProgress(progressCount);

                    var node = new GcRootPathNode
                    {
                        ObjectAddress = $"0x{address:X16}",
                        TypeName = type?.Name ?? "?",
                        RootKind = "",
                        Depth = currentParent.Depth + 1,
                    };

                    var refs = FindReferencing(true, address);
                    if (refs != null)
                    {
                        foreach (var res in refs)
                        {
                            node.ReferencingObjects.Add(new GcReferenceInfo
                            {
                                Address = $"0x{res.address:X16}",
                                TypeName = res.typeName,
                                FieldName = res.fieldName,
                                IsStatic = res.isStatic,
                            });
                        }
                    }

                    currentParent.Children.Add(node);
                    currentParent = node;
                }

                result.Paths.Add(rootNode);
            }

            result.TotalPaths = allPaths.Count;
            result.TotalReferences = progressCount;
            return result;
        }, cancellationToken);
    }

    /// <summary>
    /// Create a long string with all the possible paths from the object to its roots
    /// </summary>
    /// <param name="clrObject">The object to analyze</param>
    /// <param name="onProgress">The progress of the operation. The total number is given by GetGraphPathsCount</param>
    /// <returns></returns>
    public string PrintRoots(ClrObject clrObject, Action<int> onProgress, CancellationToken cancellationToken)
    {
        StringBuilder sb = new StringBuilder();
        var objectType = clrObject.Type;
        if (objectType == null) return string.Empty;
        sb.AppendLine($"{objectType.Name} Addr:0x{clrObject.Address:X} MT:0x{objectType.MethodTable:X} Size:{clrObject.Size}");
        sb.AppendLine();
        var roots = GetRootPaths(clrObject);
        int i = 0;
        int count = 0;
        foreach (var tplRoot in roots)
        {
            string rootKindLabel;
            string rootAddr;
            string rootTypeName;

            if (tplRoot.Root != null)
            {
                rootKindLabel = tplRoot.Root.Address == 0
                    ? "Register"
                    : tplRoot.Root.RootKind.ToString();
                rootAddr = $"0x{tplRoot.Root.Address:X16}";
                rootTypeName = tplRoot.Root.Object.Type?.Name ?? "?";
            }
            else
            {
                rootKindLabel = tplRoot.StaticField != null
                    ? $"Static {tplRoot.StaticField.Type?.Name ?? "?"}.{tplRoot.StaticField.Name ?? "?"}"
                    : "StaticField";
                var firstLink = tplRoot.Path;
                rootAddr = firstLink != null
                    ? $"0x{firstLink.Object:X16}"
                    : "0x????????????????";
                rootTypeName = firstLink != null
                    ? _clrRuntime.Heap.GetObjectType(firstLink.Object)?.Name ?? "?"
                    : "?";
            }

            sb.AppendLine($"Root {rootKindLabel} Addr:{rootAddr} {rootTypeName} ");

            // new in v3
            var path = tplRoot.Path;

            sb.AppendLine($"  Path {i++}");
            //foreach (var path in tplRoot.Path)
            for (GCRoot.ChainLink? link = path; link != null; link = link.Next)
            {
                var address = link.Object;
                var type = _clrRuntime.Heap.GetObjectType(address);

                count++;
                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException("Canceled by user request");
                onProgress(count);
                sb.AppendLine($"     {address:X16} {type?.Name ?? "?"}");
                var result = FindReferencing(true, address);
                if (result != null && result.Count > 0)
                {
                    sb.AppendLine($"          Objects whose fields point to {address:X16}");
                    foreach (var res in result)
                    {
                        string isStaticString = res.isStatic ? "static" : "instance";
                        sb.AppendLine($"               {res.address:X16} Type:{res.typeName} [{isStaticString}] field:{res.fieldName}");
                    }
                }

            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    internal List<(ulong address, string typeName, string fieldName, bool isStatic)>? FindReferencing(
        bool includeInstance, params ulong[] leakedAddresses)
    {
        List<(ulong address, string typeName, string fieldName, bool isStatic)>? result = null;

        if (includeInstance)
        {
            foreach (var (obj, field, address) in ObjectsWithInstanceFields)
            {
                if (leakedAddresses.Contains(address))
                {
                    if (result == null)
                        result = new List<(ulong address, string typeName, string fieldName, bool isStatic)>();

                    result.Add((obj.Address, obj.Type?.Name ?? "?", field?.Name ?? "?", false));
                }
            }
        }

        foreach (var (obj, field, address) in ObjectsWithStaticFields)
        {
            // returns zero if it is not an ulong
            if (leakedAddresses.Contains(address))
            {
                if (result == null)
                    result = new List<(ulong address, string typeName, string fieldName, bool isStatic)>();

                result.Add((obj.Address, obj.Type?.Name ?? "?", field?.Name ?? "?", true));
            }
        }

        return result;
    }

    /// <summary>
    /// Finds objects directly referenced by the given source addresses (walks forward).
    /// </summary>
    /// <param name="includeStatic">Whether to include static fields of the source object's type.</param>
    /// <param name="sourceAddresses">Addresses of the source objects to walk forward from.</param>
    /// <returns>
    /// A list of tuples containing the referenced object's address, its type name,
    /// the field name on the source object, and whether the field is static;
    /// or <see langword="null"/> if no references were found.
    /// </returns>
    internal List<(ulong address, string typeName, string fieldName, bool isStatic)>? FindReferenced(
        bool includeStatic, params ulong[] sourceAddresses)
    {
        List<(ulong address, string typeName, string fieldName, bool isStatic)>? result = null;

        foreach (var sourceAddress in sourceAddresses)
        {
            var obj = _clrRuntime.Heap.GetObject(sourceAddress);
            if (obj.IsNull || obj.Type is null)
                continue;

            // Instance fields
            foreach (var reference in obj.EnumerateReferencesWithFields(false, true))
            {
                if (reference.Object.IsNull)
                    continue;

                result ??= new List<(ulong address, string typeName, string fieldName, bool isStatic)>();
                result.Add((
                    reference.Object.Address,
                    reference.Object.Type?.Name ?? "?",
                    reference.Field?.Name ?? "?",
                    false));
            }

            if (includeStatic)
            {
                foreach (var field in obj.Type.StaticFields)
                {
                    if (!field.IsObjectReference)
                        continue;

                    var address = field.Read<ulong>(MainAppDomain);
                    if (address == 0)
                        continue;

                    var refObj = _clrRuntime.Heap.GetObject(address);
                    result ??= new List<(ulong address, string typeName, string fieldName, bool isStatic)>();
                    result.Add((
                        address,
                        refObj.Type?.Name ?? "?",
                        field.Name ?? "?",
                        true));
                }
            }
        }

        return result;
    }

    public void PrintClrStack()
    {
        var stacks = this.Stacks();
        foreach (var stack in stacks)
        {
            Console.WriteLine($"OS Thread Id: 0x{stack.thread.OSThreadId:x} ({stack.thread.ManagedThreadId})");
            Console.WriteLine("        Child SP               IP Call Site");
            foreach (var frame in stack.stackFrames)
            {
                var callSite = frame.FrameName == null
                    ? "" :
                    $"[{frame.FrameName} {frame.StackPointer:X16}]";


                //var il = _dataTarget.DataReader.
                //var x = frame.Method.Type!.Module.
                //var mi = frame.Method.Type!.Module.MetadataImport;
                //if (mi != null)
                //{

                //}

                Console.WriteLine($"{frame.StackPointer:X16} {frame.InstructionPointer:X16} {callSite} {frame.Method?.ToString()}");
            }
        }
    }

}

