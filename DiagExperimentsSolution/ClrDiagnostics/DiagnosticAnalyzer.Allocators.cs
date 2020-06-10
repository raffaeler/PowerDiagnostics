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
        public IDictionary<AllocatorInfo, List<ClrObject>> GetAllObjectsGroupedByAllocator(Func<ClrObject, bool> predicate)
        {
            return _clrRuntime.Heap.EnumerateObjects()
                .Where(predicate)
                .Select(o => (Object: o,
                              AllocatorInfo: Create(o.Type.LoaderAllocatorHandle)))
                .GroupBy(g => g.AllocatorInfo, g => g.Object, new AllocatorInfoComparer())
                .ToDictionary(g => g.Key, g => g.ToList());

            AllocatorInfo Create(ulong allocatorHandle)
            {
                IDataReader dataReader = _dataTarget.DataReader;
                if (allocatorHandle == 0) return AllocatorInfo.Default;
                var loaderAllocatorAddress = dataReader.ReadPointer(allocatorHandle);
                var allocator = _clrRuntime.Heap.GetObject(loaderAllocatorAddress);
                return AllocatorInfo.Create(allocator);

                //var allocatorScout = allocator.ReadObjectField("m_scout");
                //var ptrNativeLoaderAllocator = allocatorScout.ReadValueTypeField("m_nativeLoaderAllocator");

                ////var m_methodInstantiations = allocator.ReadValueTypeField("m_methodInstantiations");
                ////var m_slotsUsed = allocator.ReadValueTypeField("m_slotsUsed");
                //var reader = _clrRuntime.DataTarget.DataReader;
                //var ptrValue = reader.ReadPointer(ptrNativeLoaderAllocator.Address);
                //var buf = new Span<byte>(new byte[1024]);
                //var mem = reader.Read(ptrValue, buf, out int read);
            }
        }


    }
}
