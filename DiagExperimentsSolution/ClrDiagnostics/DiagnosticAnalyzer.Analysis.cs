using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.Diagnostics.Runtime;
using ClrDiagnostics.Extensions;
using ClrDiagnostics.Models;

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
                var result = _clrRuntime.Heap.EnumerateObjects();
                if (CacheAllObjects)
                {
                    if (_cachedAllObjects == null) _cachedAllObjects = result.ToList();
                    return _cachedAllObjects;
                }

                return result;
            }
        }

        public IEnumerable<ClrObject> ObjectReferences(ClrObject @object)
        {
            return _clrRuntime.Heap.EnumerateObjectReferences(@object.Address, @object.Type, false, true);
        }

        public IEnumerable<ClrReference> ObjectReferencesWithFields(ClrObject @object)
        {
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

        public IEnumerable<LinkedList<ClrObject>> PathsAmong(ClrObject source, ClrObject target)
        {
            return _gcroot.EnumerateAllPaths(source.Address, target.Address, false, Token);
        }


        public IEnumerable<ClrObject> GetObjectsBySize(long minSize = 1024)
        {
            return Objects
                .Where(o => o.Size > (ulong)minSize)
                .OrderByDescending(o => o.Size);
        }
    }
}
