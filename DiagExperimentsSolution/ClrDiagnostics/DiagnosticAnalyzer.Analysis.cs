using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using Microsoft.Diagnostics.Runtime;
using ClrDiagnostics.Extensions;
using ClrDiagnostics.Models;
using System.Net;
//using Microsoft.Diagnostics.Runtime.Interfaces; // required by Microsoft.Diagnostics.Runtime version 3.0

namespace ClrDiagnostics;

public partial class DiagnosticAnalyzer
{
    public IDataReader DataReader => _dataTarget.DataReader;

    public ClrAppDomain MainAppDomain => _clrRuntime.AppDomains.First();
    public IEnumerable<ClrModule> Modules => _clrRuntime.EnumerateModules();
    public IEnumerable<ClrHandle> Handles => _clrRuntime.EnumerateHandles();
    public IEnumerable<ClrThread> Threads => _clrRuntime.Threads;

    // Heap
    public IEnumerable<ClrRoot> Roots => _clrRuntime.Heap.EnumerateRoots();
    public IEnumerable<ClrRoot> FinalizerRoots => _clrRuntime.Heap.EnumerateFinalizerRoots();
    public IEnumerable<ClrObject> FinalizableObjects => _clrRuntime.Heap.EnumerateFinalizableObjects();
    public IEnumerable<ClrObject> Objects
    {
        get
        {
            if (CacheAllObjects)
            {
                if (_cachedAllObjects == null)
                    _cachedAllObjects = _clrRuntime.Heap.EnumerateObjects().ToList();
                return _cachedAllObjects;
            }

            _cachedAllObjects = null;
            return _clrRuntime.Heap.EnumerateObjects();
        }
    }

    public IEnumerable<(ClrObject, ClrInstanceField, ulong)> ObjectsWithInstanceFields
    {
        get
        {
            if (CacheAllObjects)
            {
                if (_objectsWithInstanceFields == null)
                {
                    _objectsWithInstanceFields = Objects
                            .SelectMany(o => o.Type!.Fields,
                                    (o, f) => (obj: o, @field: f))
                            .Where(t => t.@field.IsObjectReference)
                            .Select(t => (@object: t.obj, @field: t.@field, address: t.@field.Read<ulong>(t.obj.Address, false)))
                            .ToList();
                }

                return _objectsWithInstanceFields;
            }

            return Objects
                .SelectMany(o => o.Type!.Fields, (o, f) => (obj: o, @field: f))
                .Where(t => t.@field.IsObjectReference)
                .Select(t => (@object: t.obj, @field: t.@field, address: t.@field.Read<ulong>(t.obj.Address, false)));
        }
    }

    public IEnumerable<(ClrObject, ClrStaticField, ulong)> ObjectsWithStaticFields
    {
        get
        {
            if (CacheAllObjects)
            {
                if (_objectsWithStaticFields == null)
                {
                    _objectsWithStaticFields = Objects
                        .SelectMany(o => o.Type!.StaticFields, (o, f) => (obj: o, @field: f))
                        .Where(t => t.@field.IsObjectReference)
                        .Select(t => (@object: t.obj, @field: t.@field, address: t.@field.Read<ulong>(MainAppDomain)))
                        .ToList();
                }

                return _objectsWithStaticFields;
            }

            return Objects
                .SelectMany(o => o.Type!.StaticFields, (o, f) => (obj: o, @field: f))
                .Where(t => t.@field.IsObjectReference)
                .Select(t => (@object: t.obj, @field: t.@field, address: t.@field.Read<ulong>(MainAppDomain)));

        }
    }

    // Walk down the graph starting from the given object
    public IEnumerable<ClrObject> ObjectReferences(ClrObject @object)
    {
        // required by Microsoft.Diagnostics.Runtime version 3.0
        return @object.EnumerateReferences(false, true);

        // v2:
        //return _clrRuntime.Heap.EnumerateObjectReferences(@object.Address, @object.Type, false, true);
    }

    public IEnumerable<ClrReference> ObjectReferencesWithFields(ClrObject @object)
    {
        // required by Microsoft.Diagnostics.Runtime version 3.0
        return @object.EnumerateReferencesWithFields(false, true);

        // v2
        //return _clrRuntime.Heap.EnumerateReferencesWithFields(@object.Address, @object.Type, false, true);
    }

    public IEnumerable<(ClrRoot Root, GCRoot.ChainLink Path)> RootPaths(ClrObject @object)
    {
        var address = @object.Address;
        GCRoot gcroot = new(_clrRuntime.Heap, (ClrObject o) => o.Address == address);
        var paths = SafeEnumerateRootPaths(
            gcroot, address, DeduplicateRegisterRoots, Token).ToList();

        // .NET 10+ static field fallback: when GCRoot returns empty, trace upward
        // through instance+static field references via FindReferencing.
        if (paths.Count == 0)
        {
            var fallback = BuildStaticRootPaths(address, @object, Token);
            return fallback;
        }

        return paths;
    }

    [Obsolete("Use RootPaths(ClrObject) instead")]
    public IEnumerable<(ClrRoot Root, GCRoot.ChainLink Path)> RootPaths(ulong address)
    {
        var gcroot = CreateGCRoot(address);
        return SafeEnumerateRootPaths(gcroot, address, DeduplicateRegisterRoots, Token);
    }

    /// <summary>
    /// Diagnostic helper that prints key GCRoot state for a target address.
    /// Returns a multi-line string suitable for console output.
    /// </summary>
    public string DiagnoseGCRoot(ulong targetAddress)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"--- GCRoot Diagnostics for 0x{targetAddress:X16} ---");

        // 1. Object on heap?
        var obj = _clrRuntime.Heap.GetObject(targetAddress);
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
            allRoots = _clrRuntime.Heap.EnumerateRoots().ToList();
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
        var refs = FindReferencing(true, targetAddress);
        sb.AppendLine($"Objects referencing target (FindReferencing): {refs?.Count ?? 0}");
        if (refs != null)
        {
            foreach (var (addr, type, field, isStatic) in refs.Take(5))
                sb.AppendLine($"  0x{addr:X16} Type={type} Field={field} IsStatic={isStatic}");
        }

        // 5. GCRoot — both constructors
        try
        {
            var gcrootPred = new GCRoot(_clrRuntime.Heap, (ClrObject o) => o.Address == targetAddress);
            var pathsPred = gcrootPred.EnumerateRootPaths(CancellationToken.None).ToList();
            sb.AppendLine($"GCRoot (Predicate ctor): {pathsPred.Count} paths");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"GCRoot (Predicate ctor) THREW: {ex.GetType().Name}: {ex.Message}");
        }

        try
        {
            var gcrootArray = new GCRoot(_clrRuntime.Heap, new[] { targetAddress });
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

    private static IEnumerable<(ClrRoot Root, GCRoot.ChainLink Path)> SafeEnumerateRootPaths(
        GCRoot gcroot, ulong targetAddress, bool deduplicateRegisterRoots, CancellationToken cancellationToken)
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
            var all = gcroot.EnumerateRootPaths(cancellationToken).ToList();
            return FilterAndDeduplicatePaths(all, deduplicateRegisterRoots);
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
    /// Filters out register roots (Address == 0) and deduplicates when
    /// <paramref name="deduplicateRegisterRoots"/> is <see langword="true"/>.
    /// Otherwise returns the input unchanged.
    /// </summary>
    internal static IEnumerable<(ClrRoot Root, GCRoot.ChainLink Path)> FilterAndDeduplicatePaths(
        List<(ClrRoot Root, GCRoot.ChainLink Path)> all, bool deduplicateRegisterRoots)
    {
        if (!deduplicateRegisterRoots)
            return all;

        var memoryRoots = all.Where(r => r.Root.Address != 0).ToList();
        return DeduplicatePaths(memoryRoots);
    }

    /// <summary>
    /// Removes duplicate GC root paths. ClrMD's <see cref="GCRoot.EnumerateRootPaths"/>
    /// may return the same root (or equal roots) more than once in some scenarios,
    /// producing tuples with identical addresses.
    /// <para>
    /// Deduplication is based on <see cref="ClrRoot.RootKind"/> and the chain of objects
    /// from the root to the target. Different root storage addresses (e.g. two stack slots
    /// pointing to the same object) are treated as duplicates because the diagnostic value
    /// is in the unique path, not in how many locations reference it.
    /// </para>
    /// </summary>
    internal static List<(ClrRoot Root, GCRoot.ChainLink Path)> DeduplicatePaths(
        List<(ClrRoot Root, GCRoot.ChainLink Path)> paths)
    {
        if (paths.Count < 2)
            return paths;

        var seen = new HashSet<string>(paths.Count);
        var result = new List<(ClrRoot Root, GCRoot.ChainLink Path)>(paths.Count);

        foreach (var item in paths)
        {
            var key = GetPathKey(item.Root, item.Path);
            if (seen.Add(key))
                result.Add(item);
        }

        return result;
    }

    internal static string GetPathKey(ClrRoot root, GCRoot.ChainLink path)
    {
        var sb = new StringBuilder();
        sb.Append(((int)root.RootKind).ToString());
        sb.Append('|');
        for (var link = path; link != null; link = link.Next)
        {
            sb.Append(link.Object.ToString("X16"));
            sb.Append(',');
        }
        return sb.ToString();
    }

    // EnumerateAllPaths — no direct replacement in v3
    //
    //public IEnumerable<LinkedList<ClrObject>> PathsBetween(ClrObject source, ClrObject target)
    //{
    //    return _gcroot.EnumerateAllPaths(source.Address, target.Address, false, Token);
    //}

    public IEnumerable<(ClrThread thread, IEnumerable<ClrStackFrame> stackFrames)> Stacks()
    {
        return _clrRuntime.Threads
            //.Where(t => t.IsAlive && !t.IsFinalizer && t.ManagedThreadId > 0)
            .Select(t => (t, t.EnumerateStackTrace()));
    }


    public IEnumerable<ClrObject> GetObjectsBySize(long minSize = 1024, bool excludeFreeBlocks = true)
    {
        return Objects
            .Where(o => o.Size > (ulong)minSize)
            .Where(o => !o.IsFree || !excludeFreeBlocks)
            .OrderByDescending(o => o.Size);
    }

    public byte[] ReadRawContent(ClrObject @object)
    {
        var address = @object.Address;
        var length = @object.Size;
        if (@object.IsArray && @object.Type!.Name == "Byte")
        {
            var arr = @object.AsArray();
            var bytes = arr.ReadValues<byte>(0, arr.Length);
            return bytes!;
        }


        byte[] blob = new byte[length];
        _dataTarget.DataReader.Read(address, blob);
        return blob;
    }
}

