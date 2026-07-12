using DiagnosticModels;

using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Interfaces;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace ClrDiagnostics.Experimental;

public class DiagnosticAnalyzerHelper
{
    public DiagnosticAnalyzerHelper(DiagnosticAnalyzer analyzer)
    {
        this.Analyzer = analyzer;
    }

    public DiagnosticAnalyzer Analyzer { get; }

    /// <summary>
    /// Diagnostic helper that prints key GCRoot state for a target address.
    /// Returns a multi-line string suitable for console output.
    /// </summary>
    [Obsolete("This is a test waiting for response in the official clrmd / Microsoft.Diagnostics.Runtime repository.")]
    public string DiagnoseGCRoot(ulong targetAddress)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"--- GCRoot Diagnostics for 0x{targetAddress:X16} ---");

        // 1. Object on heap?
        var obj = Analyzer.ClrRuntime.Heap.GetObject(targetAddress);
        sb.AppendLine($"Object on heap: {(obj.IsNull ? "NULL" : $"0x{obj.Address:X16} Type={obj.Type?.Name ?? "?"} Size={obj.Size} ContainsPointers={obj.Type?.ContainsPointers}")}");

        if (obj.IsNull || obj.Type is null)
        {
            sb.AppendLine(">>> ROOT CAUSE: Target address does not point to a valid managed object.");
            return sb.ToString();
        }

        // 2. Heap roots — categorize and count
        List<ClrRoot> allRoots;
        try
        {
            allRoots = Analyzer.ClrRuntime.Heap.EnumerateRoots().ToList();
        }
        catch (Exception ex)
        {
            sb.AppendLine($"EnumerateRoots FAILED: {ex.GetType().Name}: {ex.Message}");
            return sb.ToString();
        }
        sb.AppendLine($"Total GC roots: {allRoots.Count}");

        var rootsByKind = allRoots
            .GroupBy(r => r.RootKind)
            .ToDictionary(g => g.Key, g => g.ToList());
        sb.AppendLine("Roots by kind:");
        foreach (var kv in rootsByKind.OrderByDescending(kv => kv.Value.Count))
            sb.AppendLine($"  {kv.Key}: {kv.Value.Count}");

        // 3. Direct root hits: which roots point directly to the target?
        var directHits = allRoots.Where(r => r.Object.Address == targetAddress).ToList();
        sb.AppendLine($"Roots directly pointing to target: {directHits.Count}");
        foreach (var r in directHits.Take(5))
            sb.AppendLine($"  Root 0x{r.Address:X16} Kind={r.RootKind}");

        // 4. Objects referencing the target (FindReferencing - already implemented)
        var refs = Analyzer.FindReferencing(true, targetAddress);
        sb.AppendLine($"Objects referencing target (FindReferencing): {refs?.Count ?? 0}");
        if (refs != null)
        {
            foreach (var (addr, type, field, isStatic) in refs.Take(5))
                sb.AppendLine($"  0x{addr:X16} Type={type} Field={field} IsStatic={isStatic}");
        }

        // 5. GCRoot — both constructors
        try
        {
            var gcrootPred = new GCRoot(Analyzer.ClrRuntime.Heap, (ClrObject o) => o.Address == targetAddress);
            var pathsPred = gcrootPred.EnumerateRootPaths(CancellationToken.None).ToList();
            sb.AppendLine($"GCRoot (Predicate ctor): {pathsPred.Count} paths");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"GCRoot (Predicate ctor) THREW: {ex.GetType().Name}: {ex.Message}");
        }

        try
        {
            var gcrootArray = new GCRoot(Analyzer.ClrRuntime.Heap, new[] { targetAddress });
            var pathsArray = gcrootArray.EnumerateRootPaths(CancellationToken.None).ToList();
            sb.AppendLine($"GCRoot (ulong[] ctor):  {pathsArray.Count} paths");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"GCRoot (ulong[] ctor) THREW: {ex.GetType().Name}: {ex.Message}");
        }

        // 6. Verdict
        if (allRoots.Count == 0)
            sb.AppendLine(">>> ROOT CAUSE: ZERO GC roots. The DataTarget does not expose roots (wrong dump format, missing SOS, or architecture mismatch).");
        else if (directHits.Count > 0)
            sb.AppendLine($">>> ROOT CAUSE: {directHits.Count} roots point directly to the target but GCRoot returned no paths. This is a ClrMD bug.");
        else if ((refs?.Count ?? 0) > 0)
            sb.AppendLine($">>> ({refs!.Count}) objects REFERENCE the target (found via FindReferencing) but GCRoot found no paths. The target is reachable in the object graph but ClrMD's GCRoot cannot trace back to a root.");
        else
            sb.AppendLine(">>> ROOT CAUSE: The object IS on the heap but NO root points to it (directly or indirectly). It is a DEAD object — GC has collected it but not yet swept. The dump was taken after the object became unreachable.");

        return sb.ToString();
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
            var targetObj = Analyzer.ClrRuntime.Heap.GetObject(targetAddress);
            if (targetObj.IsNull || targetObj.Type is null)
                return result;

            // Create GCRoot per-call (not thread-safe) and enumerate paths directly
            // using the method's cancellationToken rather than the analyzer's global Token.
            var gcroot = new GCRoot(Analyzer.ClrRuntime.Heap, (ClrObject o) => o.Address == targetAddress);
            var allPaths = SafeEnumerateRootPaths(
                gcroot, targetAddress, cancellationToken);

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
                    var type = Analyzer.ClrRuntime.Heap.GetObjectType(address);

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

                    var refs = Analyzer.FindReferencing(true, address);
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

                    var refs = Analyzer.FindReferencing(true, targetAddress);
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

        // Post-processing: if the chain terminates at an array (e.g.
        // GraphRoot[] backed by List<GraphRoot>._items), try to extend
        // it upward by finding the object that owns the array.  This is
        // a belt-and-suspenders fallback on top of TraceSingleChain's
        // own FindArrayOwner call, in case that call was not compiled
        // into the running binary or failed for some reason.
        var visitedSet = new HashSet<ulong> { targetAddress };
        foreach (var e in chain)
            visitedSet.Add(e.address);

        const int maxExtendIterations = 10;
        for (int extend = 0; extend < maxExtendIterations; extend++)
        {
            var chainTop = chain[chain.Count - 1].address;
            var chainTopType = Analyzer.ClrRuntime.Heap.GetObjectType(chainTop);
            if (chainTopType == null || !chainTopType.IsArray)
                break;

            var owners = Analyzer.FindReferencing(true, chainTop);
            if (owners == null || owners.Count == 0)
                owners = FindArrayOwner(chainTop);
            if (owners == null || owners.Count == 0)
                break;

            var owner = owners[0];
            if (!visitedSet.Add(owner.address))
                break;

            chain.Add(owner);
        }

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
            topObj = Analyzer.ClrRuntime.Heap.GetObject(topAddress);
        }
        else if (buildLength > 0)
        {
            topAddress = chain[buildLength - 1].address;
            topObj = Analyzer.ClrRuntime.Heap.GetObject(topAddress);
        }
        else
        {
            topAddress = targetAddress;
            topObj = targetObj;
        }

        // Try to find a real ClrRoot for the top, otherwise create a synthetic one
        ClrRoot? matchingRoot = null;
        foreach (var r in Analyzer.ClrRuntime.Heap.EnumerateRoots())
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
        var refs = Analyzer.FindReferencing(true, address);

        // If nothing found via named fields, try to find the array that contains
        // this address as an element (list/array element case).
        (ulong, string, string, bool)? arrayRef = null;
        if (refs == null || refs.Count == 0)
        {
            arrayRef = FindSingleArrayContainer(address);
        }

        // Fallback: if neither FindReferencing nor FindSingleArrayContainer found
        // a parent, and the address is itself an array, scan the heap directly for
        // objects whose fields reference this array (e.g. List<T>._items → T[]).
        // This handles the case where ObjectsWithInstanceFields cache may not
        // include the owning object's field pointing to the array.
        if ((refs == null || refs.Count == 0) && arrayRef == null)
        {
            var arrayOwner = FindArrayOwner(address);
            if (arrayOwner != null && arrayOwner.Count > 0)
            {
                refs = arrayOwner;
            }
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
        foreach (var obj in Analyzer.Objects)
        {
            if (!obj.IsArray || obj.IsNull)
                continue;
            if (obj.Type?.ComponentType is null || !obj.Type.ComponentType.IsObjectReference)
                continue;

            var array = obj.AsArray();
            var length = array.Length;
            if (length > 10_000)        // Better not
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

    /// <summary>
    /// Directly scans all heap objects for instance fields whose value points to
    /// <paramref name="arrayAddress"/>.  This is a fallback for
    /// <see cref="FindReferencing"/> when the cached
    /// <see cref="ObjectsWithInstanceFields"/> does not contain the owning object's
    /// field (e.g. <c>List&lt;T&gt;._items</c> → <c>T[]</c>).
    /// Used only when <paramref name="arrayAddress"/> is itself an array and the
    /// normal back-reference lookups have failed.
    /// </summary>
    private List<(ulong address, string typeName, string fieldName, bool isStatic)>? FindArrayOwner(
        ulong arrayAddress)
    {
        // Quick sanity check: is this actually an array?
        var arrayType = Analyzer.ClrRuntime.Heap.GetObjectType(arrayAddress);
        if (arrayType == null || !arrayType.IsArray)
            return null;

        List<(ulong, string, string, bool)>? result = null;

        foreach (var obj in Analyzer.Objects)
        {
            if (obj.IsNull || obj.Type is null)
                continue;

            // Skip arrays themselves — only non-array objects can own an array.
            if (obj.Type.IsArray)
                continue;

            foreach (var field in obj.Type.Fields)
            {
                try
                {
                    var fieldValue = field.Read<ulong>(obj.Address, false);
                    if (fieldValue == arrayAddress)
                    {
                        result ??= new List<(ulong, string, string, bool)>();
                        result.Add((obj.Address, obj.Type.Name ?? "?", field.Name ?? "?", false));
                    }
                }
                catch
                {
                    // Some fields cannot be read as ulong (e.g. value types,
                    // static-only fields on constructed generics).  Skip.
                }
            }
        }

        return result;
    }

    private void WalkForward(
        GcRootPathNode parent,
        ulong parentAddress,
        HashSet<ulong> visited,
        ref int progressCount,
        Action<int> onProgress,
        CancellationToken cancellationToken)
    {
        var refs = Analyzer.FindReferenced(false, parentAddress);
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

            var backRefs = Analyzer.FindReferencing(true, res.address);
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

    internal static IEnumerable<(ClrRoot Root, GCRoot.ChainLink Path)> SafeEnumerateRootPaths(
    GCRoot gcroot, ulong targetAddress, CancellationToken cancellationToken)
    {
        // GCRoot.EnumerateRootPaths walks the heap from each GC root to the target(s).
        // ClrMD v4's multi-threaded root enumeration can throw when the heap is in an
        // inconsistent state (e.g. corrupted dump files): InvalidOperationException,
        // ObjectDisposedException, or ArgumentException.
        //
        // We catch ONLY those known ClrMD exceptions and return empty. Everything else
        // (NullReferenceException, user-code bugs, etc.) propagates to the caller so
        // the error is visible.
        try
        {
            return gcroot.EnumerateRootPaths(cancellationToken).ToList();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is InvalidOperationException
                                or ObjectDisposedException
                                or ArgumentException)
        {
            Trace.WriteLine(
                $"[ClrDiagnostics] GCRoot enumeration failed for object 0x{targetAddress:X16}: {ex.Message}");
            return Enumerable.Empty<(ClrRoot, GCRoot.ChainLink)>();
        }
    }

    /// <summary>
    /// Gets the objects directly referenced by the given target address (1 level only).
    /// Returns a single-path tree: target node → its direct children.
    /// Each child carries its field name in <see cref="GcRootPathNode.ReferencingObjects"/>.
    /// </summary>
    /// <param name="targetAddress">The object address to find forward references for.</param>
    /// <param name="maxRefs">Maximum number of references to return (default 5000).
    /// Prevents browser hangs when an object has thousands of field references.</param>
    /// <returns>A <see cref="GcRootPathResult"/> with a single path containing the target
    /// and its direct children, or an empty result if the address is invalid.</returns>
    public GcRootPathResult GetForwardReferences(ulong targetAddress, int maxRefs = 5000)
    {
        var result = new GcRootPathResult();
        var targetObj = Analyzer.ClrRuntime.Heap.GetObject(targetAddress);
        if (targetObj.IsNull || targetObj.Type is null)
            return result;

        var rootNode = new GcRootPathNode
        {
            ObjectAddress = $"0x{targetAddress:X16}",
            TypeName = targetObj.Type?.Name ?? "?",
            RootKind = "",
            Depth = 0,
        };

        var refs = Analyzer.FindReferenced(false, targetAddress);
        if (refs != null)
        {
            int count = 0;
            foreach (var res in refs)
            {
                if (count >= maxRefs)
                    break;

                var childNode = new GcRootPathNode
                {
                    ObjectAddress = $"0x{res.address:X16}",
                    TypeName = res.typeName,
                    RootKind = "",
                    Depth = 1,
                };

                // Embed the field name as a back-reference so GcRootTree can display it
                childNode.ReferencingObjects.Add(new GcReferenceInfo
                {
                    Address = $"0x{targetAddress:X16}",
                    TypeName = rootNode.TypeName,
                    FieldName = res.fieldName,
                    IsStatic = false,
                });

                rootNode.Children.Add(childNode);
                count++;
            }
        }

        result.Paths.Add(rootNode);
        result.TotalPaths = 1;
        result.TotalReferences = rootNode.Children.Count;
        return result;
    }
}
