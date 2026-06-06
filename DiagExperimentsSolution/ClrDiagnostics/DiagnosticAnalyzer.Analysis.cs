using ClrDiagnostics.Extensions;
using ClrDiagnostics.Models;

using Microsoft.Diagnostics.Runtime;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;

using static Microsoft.Diagnostics.Runtime.GCRoot;
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

    /// <summary>
    /// Given an object, returns the static field and the complete path that
    /// keeps it alive, starting from the static field reference and ending
    /// with the given object.
    /// </summary>
    /// <param name="clrObject">The object that we are trying to find paths for</param>
    /// <returns>The path for each static field, if any</returns>
    public IEnumerable<(ClrStaticField, ChainLink)> FindPathsToStatics(ClrObject clrObject,
        CancellationToken token = default)
    {
        var staticFields = GetStaticFields()
            .ToList();

        List<(ClrStaticField, ChainLink)> results = [];
        foreach (var root in staticFields)
        {
            token.ThrowIfCancellationRequested();

            GCRoot gcRoot = new(Heap, [clrObject.Address]);
            ChainLink? link = gcRoot.FindPathFrom(root.obj);
            if (link is null) continue;
            results.Add((root.field, link));
        }

        return results;
    }

    public IList<(ClrRoot? Root, ClrStaticField? StaticField, GCRoot.ChainLink Path)> GetRootPaths(ClrObject @object)
    {
        List<(ClrRoot? Root, ClrStaticField? StaticField, GCRoot.ChainLink Path)> paths = [];

        var address = @object.Address;
        GCRoot gcroot = new(_clrRuntime.Heap, (ClrObject o) => o.Address == address);

        paths = gcroot.EnumerateRootPaths(Token)
            .Select(x => (Root: (ClrRoot?)x.Root,
                          StaticField: (ClrStaticField?)null,
                          Path: x.Path))
            .ToList();

        // .NET 10+ static field fallback: when GCRoot returns empty, trace upward
        // through instance+static field references via FindReferencing.
        paths.AddRange(FindPathsToStatics(@object, Token)
            .Select(t => (Root: (ClrRoot?)null,
                          StaticField: (ClrStaticField?)t.Item1,
                          Path: t.Item2)));

        return paths;
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

