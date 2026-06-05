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
        var gcroot = CreateGCRoot(@object.Address);
        return SafeEnumerateRootPaths(gcroot, @object.Address, DeduplicateRegisterRoots, Token);
    }

    [Obsolete("Use RootPaths(ClrObject) instead")]
    public IEnumerable<(ClrRoot Root, GCRoot.ChainLink Path)> RootPaths(ulong address)
    {
        var gcroot = CreateGCRoot(address);
        return SafeEnumerateRootPaths(gcroot, address, DeduplicateRegisterRoots, Token);
    }

    private static IEnumerable<(ClrRoot Root, GCRoot.ChainLink Path)> SafeEnumerateRootPaths(
        GCRoot gcroot, ulong targetAddress, bool deduplicateRegisterRoots, CancellationToken cancellationToken)
    {
        // GCRoot can throw when the heap is in an inconsistent state or
        // when walking certain corrupted objects (ClrMD known issue).
        // We use ToList() to eagerly enumerate so exceptions during the walk
        // are caught here instead of propagating to the caller.
        try
        {
            var all = gcroot.EnumerateRootPaths(cancellationToken).ToList();
            return FilterAndDeduplicatePaths(all, deduplicateRegisterRoots);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ClrDiagnostics] GCRoot enumeration failed for object 0x{targetAddress:X16}: {ex.Message}");
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

