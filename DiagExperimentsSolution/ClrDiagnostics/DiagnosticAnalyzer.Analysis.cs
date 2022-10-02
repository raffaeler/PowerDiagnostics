using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.Diagnostics.Runtime;
using ClrDiagnostics.Extensions;
using ClrDiagnostics.Models;
using System.Net;

namespace ClrDiagnostics
{
    public partial class DiagnosticAnalyzer
    {
        public IDataReader DataReader => _dataTarget.DataReader;

        public ClrAppDomain MainAppDomain => _clrRuntime.AppDomains.First();
        public IEnumerable<ClrModule> Modules => _clrRuntime.EnumerateModules();
        public IEnumerable<ClrHandle> Handles => _clrRuntime.EnumerateHandles();
        public IEnumerable<ClrThread> Threads => _clrRuntime.Threads;

        // Heap
        public IEnumerable<IClrRoot> Roots => _clrRuntime.Heap.EnumerateRoots();
        public IEnumerable<IClrRoot> FinalizerRoots => _clrRuntime.Heap.EnumerateFinalizerRoots();
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
                                .SelectMany(o => o.Type.Fields, (o, f) => (obj: o, field: f))
                                .Where(t => t.field.IsObjectReference)
                                .Select(t => (@object: t.obj, field: t.field, address: t.field.Read<ulong>(t.obj.Address, false)))
                                .ToList();
                    }

                    return _objectsWithInstanceFields;
                }

                return Objects
                    .SelectMany(o => o.Type.Fields, (o, f) => (obj: o, field: f))
                    .Where(t => t.field.IsObjectReference)
                    .Select(t => (@object: t.obj, field: t.field, address: t.field.Read<ulong>(t.obj.Address, false)));
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
                            .SelectMany(o => o.Type.StaticFields, (o, f) => (obj: o, field: f))
                            .Where(t => t.field.IsObjectReference)
                            .Select(t => (@object: t.obj, field: t.field, address: t.field.Read<ulong>(MainAppDomain)))
                            .ToList();
                    }

                    return _objectsWithStaticFields;
                }

                return Objects
                    .SelectMany(o => o.Type.StaticFields, (o, f) => (obj: o, field: f))
                    .Where(t => t.field.IsObjectReference)
                    .Select(t => (@object: t.obj, field: t.field, address: t.field.Read<ulong>(MainAppDomain)));

            }
        }

        // Walk down the graph starting from the given object
        public IEnumerable<ClrObject> ObjectReferences(ClrObject @object)
        {
            return _clrRuntime.Heap.EnumerateObjectReferences(@object.Address, @object.Type, false, true);
        }

        public IEnumerable<ClrReference> ObjectReferencesWithFields(ClrObject @object)
        {
            //return @object.EnumerateReferencesWithFields(false, true);
            return _clrRuntime.Heap.EnumerateReferencesWithFields(@object.Address, @object.Type, false, true);
        }

        public IEnumerable<GCRootPath> RootPaths(ClrObject @object)
        {
            return RootPaths(@object.Address);
        }

        public IEnumerable<GCRootPath> RootPaths(ulong address)
        {
            return _gcroot.EnumerateGCRoots(address, false, Token);
        }

        public IEnumerable<LinkedList<ClrObject>> PathsBetween(ClrObject source, ClrObject target)
        {
            return _gcroot.EnumerateAllPaths(source.Address, target.Address, false, Token);
        }

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
            if (@object.IsArray && @object.Type.Name == "Byte")
            {
                var arr = @object.AsArray();
                var bytes = arr.ReadValues<byte>(0, arr.Length);
                return bytes;
            }


            byte[] blob = new byte[length];
            _dataTarget.DataReader.Read(address, blob);
            return blob;
        }
    }
}
