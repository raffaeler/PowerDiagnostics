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
        var roots = RootPaths(clrObject);
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

            // Create GCRoot per-call (not thread-safe) and enumerate paths directly
            // using the method's cancellationToken rather than the analyzer's global Token.
            var targetAddr = clrObject.Address;
            var gcroot = new GCRoot(_clrRuntime.Heap, (ClrObject o) => o.Address == targetAddr);
            var allPaths = SafeEnumerateRootPaths(
                gcroot, targetAddr, DeduplicateRegisterRoots, cancellationToken);

            List<(ClrRoot Root, GCRoot.ChainLink Path)> roots;
            if (maxPaths == -1)
                roots = allPaths.ToList();
            else
                roots = allPaths.Take(maxPaths).ToList();

            // .NET 10+ static field fallback (same as GetAddressPathAsync)
            if (roots.Count == 0)
            {
                roots = BuildStaticRootPaths(targetAddr, clrObject, cancellationToken);
            }

            int progressCount = 0;

            foreach (var tplRoot in roots)
            {
                var rootKindLabel = tplRoot.Root.Address == 0
                    ? "Register"
                    : tplRoot.Root.RootKind.ToString();

                var rootNode = new GcRootPathNode
                {
                    ObjectAddress = $"0x{tplRoot.Root.Address:X16}",
                    TypeName = tplRoot.Root.Object.Type?.Name ?? "?",
                    RootKind = rootKindLabel,
                    Depth = 0,
                };

                GcRootPathNode currentParent = rootNode;

                for (GCRoot.ChainLink? link = tplRoot.Path; link != null; link = link.Next)
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

            result.TotalPaths = roots.Count;
            result.TotalReferences = progressCount;
            return result;
        }, cancellationToken);
    }

    /// <summary>
    /// Builds a structured GC root path tree that passes through the given target address.
    /// Walks upward from the target to its GC roots, then forward from the target
    /// to objects it references.
    /// </summary>
    /// <param name="targetAddress">The object address to center the search on.</param>
    /// <param name="onProgress">Callback fired once per chain link or forward reference processed.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <param name="maxPaths">Maximum number of root paths to enumerate.</param>
    /// <returns>A <see cref="GcRootPathResult"/> containing nested root nodes with the target in the middle.</returns>
    public Task<GcRootPathResult> GetAddressPathAsync(
        ulong targetAddress,
        Action<int> onProgress,
        CancellationToken cancellationToken,
        int maxPaths = -1)
    {
        return Task.Run(() =>
        {
            var result = new GcRootPathResult();
            var targetObj = _clrRuntime.Heap.GetObject(targetAddress);
            if (targetObj.IsNull || targetObj.Type is null)
                return result;

            // Create GCRoot per-call (not thread-safe) and enumerate paths directly
            // using the method's cancellationToken rather than the analyzer's global Token.
            var gcroot = new GCRoot(_clrRuntime.Heap, (ClrObject o) => o.Address == targetAddress);
            var allPaths = SafeEnumerateRootPaths(
                gcroot, targetAddress, DeduplicateRegisterRoots, cancellationToken);

            List<(ClrRoot Root, GCRoot.ChainLink Path)> roots;
            if (maxPaths == -1)
                roots = allPaths.ToList();
            else
                roots = allPaths.Take(maxPaths).ToList();

            // .NET 10+ stores static fields in per-module Object[] arrays that are
            // NOT exposed as ClrRoot via EnumerateRoots().  When GCRoot returns empty
            // but the object is alive via a static field chain, fall back to tracing
            // upward through FindReferencing (which scans ObjectsWithStaticFields).
            if (roots.Count == 0)
            {
                roots = BuildStaticRootPaths(targetAddress, targetObj, cancellationToken);
            }

            int progressCount = 0;

            foreach (var tplRoot in roots)
            {
                var rootKindLabel = tplRoot.Root.Address == 0
                    ? "Register"
                    : tplRoot.Root.RootKind.ToString();

                var rootNode = new GcRootPathNode
                {
                    ObjectAddress = $"0x{tplRoot.Root.Address:X16}",
                    TypeName = tplRoot.Root.Object.Type?.Name ?? "?",
                    RootKind = rootKindLabel,
                    Depth = 0,
                };

                GcRootPathNode currentParent = rootNode;

                // Walk upstream: root → ... → target
                for (GCRoot.ChainLink? link = tplRoot.Path; link != null; link = link.Next)
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

                // Ensure target is the last upstream node
                if (currentParent.ObjectAddress != $"0x{targetAddress:X16}")
                {
                    progressCount++;
                    if (cancellationToken.IsCancellationRequested)
                        throw new OperationCanceledException("Canceled by user request");

                    onProgress(progressCount);

                    var targetNode = new GcRootPathNode
                    {
                        ObjectAddress = $"0x{targetAddress:X16}",
                        TypeName = targetObj.Type?.Name ?? "?",
                        RootKind = "",
                        Depth = currentParent.Depth + 1,
                    };

                    var refs = FindReferencing(true, targetAddress);
                    if (refs != null)
                    {
                        foreach (var res in refs)
                        {
                            targetNode.ReferencingObjects.Add(new GcReferenceInfo
                            {
                                Address = $"0x{res.address:X16}",
                                TypeName = res.typeName,
                                FieldName = res.fieldName,
                                IsStatic = res.isStatic,
                            });
                        }
                    }

                    currentParent.Children.Add(targetNode);
                    currentParent = targetNode;
                }

                // Walk forward from target
                var visited = new HashSet<ulong>();
                visited.Add(targetAddress);
                WalkForward(currentParent, targetAddress, visited, ref progressCount, onProgress, cancellationToken);

                result.Paths.Add(rootNode);
            }

            result.TotalPaths = roots.Count;
            result.TotalReferences = progressCount;
            return result;
        }, cancellationToken);
    }

    /// <summary>
    /// Fallback root-path builder for objects kept alive by static fields
    /// (whose static Object[] holders are not exposed as ClrRoot in .NET 10+).
    /// Builds a SINGLE chain from the target upward through instance and static
    /// field references.  Finds the first parent at each level — no branching.
    /// </summary>
    private List<(ClrRoot Root, GCRoot.ChainLink Path)> BuildStaticRootPaths(
        ulong targetAddress,
        ClrObject targetObj,
        CancellationToken cancellationToken)
    {
        var result = new List<(ClrRoot Root, GCRoot.ChainLink Path)>();

        // Build a single chain: chain[0]=immediate parent, chain[^1]=top
        var chain = TraceSingleChain(targetAddress, new HashSet<ulong> { targetAddress }, cancellationToken);
        if (chain.Count == 0)
            return result;

        // Find the first static-field reference in the chain (closest to target).
        // chain[i].isStatic means chain[i] OWNS a static field whose value is the
        // next node down (chain[i-1] or the target).  For leak detection, the
        // static field IS the root — trim everything above it including the owner,
        // and start the chain from the static field's VALUE.
        int rootIdx = chain.Count;  // default: no static field, use full chain
        for (int i = 0; i < chain.Count; i++)
        {
            if (chain[i].isStatic)
            {
                rootIdx = i;
                break;
            }
        }

        // buildLength = number of chain entries to include in the ChainLink.
        // If we found a static field, exclude the owner (rootIdx itself) and
        // everything above it.  The ChainLink starts from the static field's VALUE.
        int buildLength = rootIdx < chain.Count ? rootIdx : chain.Count;

        // The top of the visible chain is the object the static field points to
        // (or the original top if no static field was found).
        ulong topAddress;
        ClrObject topObj;
        if (rootIdx < chain.Count && rootIdx > 0)
        {
            topAddress = chain[rootIdx - 1].address;
            topObj = _clrRuntime.Heap.GetObject(topAddress);
        }
        else if (buildLength > 0)
        {
            topAddress = chain[buildLength - 1].address;
            topObj = _clrRuntime.Heap.GetObject(topAddress);
        }
        else
        {
            topAddress = targetAddress;
            topObj = targetObj;
        }

        // Try to find a real ClrRoot for the top, otherwise create a synthetic one
        ClrRoot? matchingRoot = null;
        foreach (var r in _clrRuntime.Heap.EnumerateRoots())
        {
            if (r.Object.Address == topAddress)
            {
                matchingRoot = r;
                break;
            }
        }

        if (matchingRoot == null)
        {
            matchingRoot = new ClrRoot(
                topAddress, topObj, ClrRootKind.StrongHandle,
                isPinned: false, isInterior: false);
        }

        // Build ChainLink: top → ... → immediateParent → target → null
        GCRoot.ChainLink? path = new GCRoot.ChainLink { Object = targetAddress };
        for (int i = 0; i < buildLength; i++)
        {
            path = new GCRoot.ChainLink { Object = chain[i].address, Next = path };
        }

        result.Add((matchingRoot, path!));
        return result;
    }

    /// <summary>
    /// Traces a SINGLE upward chain from <paramref name="address"/> using the
    /// first available back-reference at each level.  Does NOT explore branches —
    /// one parent per level, greedy.
    /// Returns chain[0]=immediate parent ... chain[^1]=top.
    /// </summary>
    private List<(ulong address, string typeName, string fieldName, bool isStatic)> TraceSingleChain(
        ulong address,
        HashSet<ulong> visited,
        CancellationToken cancellationToken,
        int depth = 0)
    {
        const int maxDepth = 30;
        if (depth >= maxDepth)
            return new List<(ulong, string, string, bool)>();

        // Find objects whose named fields point to 'address'
        var refs = FindReferencing(true, address);

        // If nothing found via named fields, try to find the array that contains
        // this address as an element (list/array element case).
        (ulong, string, string, bool)? arrayRef = null;
        if (refs == null || refs.Count == 0)
        {
            arrayRef = FindSingleArrayContainer(address);
        }

        // No parent found at all — this is the top of the chain
        if ((refs == null || refs.Count == 0) && arrayRef == null)
            return new List<(ulong, string, string, bool)>();

        // Pick the first available parent (field ref takes priority over array ref)
        (ulong refAddr, string refType, string refField, bool refIsStatic) first;
        if (refs != null && refs.Count > 0)
            first = refs[0];
        else
            first = arrayRef!.Value;

        cancellationToken.ThrowIfCancellationRequested();

        if (!visited.Add(first.refAddr))
            return new List<(ulong, string, string, bool)>();

        var upper = TraceSingleChain(first.refAddr, visited, cancellationToken, depth + 1);
        visited.Remove(first.refAddr);

        var chain = new List<(ulong, string, string, bool)> { first };
        chain.AddRange(upper);
        return chain;
    }

    /// <summary>
    /// Finds a single array that contains <paramref name="targetAddress"/> as an
    /// element.  Stops at the first match (does not scan the entire heap).
    /// </summary>
    private (ulong address, string typeName, string fieldName, bool isStatic)?
        FindSingleArrayContainer(ulong targetAddress)
    {
        foreach (var obj in Objects)
        {
            if (!obj.IsArray || obj.IsNull)
                continue;
            if (obj.Type?.ComponentType is null || !obj.Type.ComponentType.IsObjectReference)
                continue;

            var array = obj.AsArray();
            var length = array.Length;
            if (length > 10_000)
                continue;

            for (int i = 0; i < length; i++)
            {
                if (array.GetObjectValue(i).Address == targetAddress)
                {
                    var typeName = obj.Type!.Name ?? "?";
                    return (obj.Address, typeName, $"[{i}]", false);
                }
            }
        }

        return null;
    }

    private void WalkForward(
        GcRootPathNode parent,
        ulong parentAddress,
        HashSet<ulong> visited,
        ref int progressCount,
        Action<int> onProgress,
        CancellationToken cancellationToken)
    {
        var refs = FindReferenced(false, parentAddress);
        if (refs == null)
            return;

        foreach (var res in refs)
        {
            progressCount++;
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException("Canceled by user request");

            onProgress(progressCount);

            var node = new GcRootPathNode
            {
                ObjectAddress = $"0x{res.address:X16}",
                TypeName = res.typeName,
                RootKind = "",
                Depth = parent.Depth + 1,
            };

            var backRefs = FindReferencing(true, res.address);
            if (backRefs != null)
            {
                foreach (var backRef in backRefs)
                {
                    node.ReferencingObjects.Add(new GcReferenceInfo
                    {
                        Address = $"0x{backRef.address:X16}",
                        TypeName = backRef.typeName,
                        FieldName = backRef.fieldName,
                        IsStatic = backRef.isStatic,
                    });
                }
            }

            parent.Children.Add(node);

            if (visited.Add(res.address))
            {
                WalkForward(node, res.address, visited, ref progressCount, onProgress, cancellationToken);
            }
        }
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
        var roots = RootPaths(clrObject);
        int i = 0;
        int count = 0;
        foreach (var tplRoot in roots)
        {
            var rootKindLabel = tplRoot.Root.Address == 0
                ? "Register"
                : tplRoot.Root.RootKind.ToString();
            sb.AppendLine($"Root {rootKindLabel} Addr:{tplRoot.Root.Address:X16} {tplRoot.Root.Object.Type?.Name ?? "?"} ");

            // new in v3
            var root = tplRoot.Root;
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

    private List<(ulong address, string typeName, string fieldName, bool isStatic)>? FindReferencing(
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
    private List<(ulong address, string typeName, string fieldName, bool isStatic)>? FindReferenced(
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

