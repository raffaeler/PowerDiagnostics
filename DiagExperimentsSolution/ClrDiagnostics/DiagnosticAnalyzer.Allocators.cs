using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.Diagnostics.Runtime;
using ClrDiagnostics.Extensions;
using ClrDiagnostics.Models;
using Microsoft.Diagnostics.Symbols;
using System.IO;
using System.Diagnostics;

namespace ClrDiagnostics
{
    public partial class DiagnosticAnalyzer
    {
        private const string _scoutField = "m_scout";
        private const string _nativeLoaderAllocatorField = "m_nativeLoaderAllocator";
        private const ulong _binderToReleaseOffset = 0x4e0;
        private const ulong _ptrManagedAssemblyLoadContextOffset = 0x160;
        private const string _nameField = "_name";

        public IEnumerable<(ClrObject allocator, IEnumerable<ClrObject> objects)> GetObjectsGroupedByAllocator(IEnumerable<ClrObject> objects)
        {
            return objects
                .Where(o => !o.IsFree && o.Type.LoaderAllocatorHandle != 0)
                .Select(o => (allocator: GetAllocatorObject(o), obj: o))
                .Where(t => t.allocator != 0)
                .GroupBy(g => g.allocator, g => g.obj)
                .Select(g => (g.Key, (IEnumerable<ClrObject>)g.ToList()));
        }

        private ClrObject GetAllocatorObject(ClrObject clrObject)
        {
            var assemblyLoadContextAddress = clrObject.Type.AssemblyLoadContextAddress;
            //if (assemblyLoadContextAddress != 0) Debugger.Break();
            var alc = _clrRuntime.Heap.GetObject(assemblyLoadContextAddress);
            return alc;
        }

        /// <summary>
        /// This only works starting from .NET 5
        /// https://github.com/dotnet/runtime/issues/11157
        /// TODO: compute the field from the property name
        /// </summary>
        /// <param name="allocatorObject"></param>
        /// <returns></returns>
        public string GetAllocatorName(ClrObject allocatorObject)
        {
            // allocatorObject is AssemblyLoadContext or a derived class
            // this means it has a property called "Name"
            var value = allocatorObject.ReadObjectField("_name");
            return value.GetStringValue();
        }

/*
        /// <summary>
        /// Experimental, may crash
        /// </summary>
        private ClrObject GetAllocatorObject(ClrObject clrObject)
        {
            var allocatorHandle = clrObject.Type.LoaderAllocatorHandle;
            var allocatorAddress = _dataTarget.DataReader.ReadPointer(allocatorHandle);
            var allocatorObject = _clrRuntime.Heap.GetObject(allocatorAddress);
            return allocatorObject;
        }
  
        /// <summary>
        /// Experimental, may crash
        /// This was the only way to make it work with dumps created in .NET Core 3.x
        /// Starting from .NET 5, the SOS8 interface has been introduced and specifically address this need.
        /// </summary>
        public string GetAllocatorName(ClrObject allocatorObject)
        {
            // Procedure described at this link:
            // https://github.com/dotnet/runtime/issues/11157#issuecomment-642288660
            //

            // The allocator object in input is obtained through GetAllocatorObject
            // Inside the allocator object there is the allocatorScout referenced by m_scout
            var allocatorScoutObject = allocatorObject.ReadObjectField(_scoutField);

            // The allocator scout gas a field called "m_nativeLoaderAllocator" (IntPtr) containing
            // the native allocator
            var nativeLoaderAllocator = allocatorScoutObject.ReadValueTypeField(_nativeLoaderAllocatorField);
            var assemblyLoaderAllocatorPtr = _dataTarget.DataReader.ReadPointer(nativeLoaderAllocator.Address);

            // Ideally we should be able to avoid hardcoding the offsets and obtain them
            // from the native PDBs
            //var offset = GetOffsetForFieldNative(assemblyLoaderAllocatorPtr, "AssemblyLoaderAllocator", "m_binderToRelease");

            // the native AssemblyLoaderAllocator native class has a field at _binderToReleaseOffset offset
            // which is caled "m_binderToRelease"
            // Native class "AssemblyLoaderAllocator", field: "m_binderToRelease" offset
            var binderToRelease = _dataTarget.DataReader.Read<ulong>(assemblyLoaderAllocatorPtr + _binderToReleaseOffset);

            // The obtained object is native, "CLRPrivBinderAssemblyLoadContext"
            // and its field at offset 0x160 (called "m_ptrManagedAssemblyLoadContext") contains
            // a table of pointers. At offset zero there is the address of "HostAssemblyLoadContext"
            // which is the managed offset AssemblyLoadContext
            // Native class: "CLRPrivBinderAssemblyLoadContext", field: "m_ptrManagedAssemblyLoadContext"
            var ptrManagedAssemblyLoadContext = _dataTarget.DataReader.Read<ulong>(binderToRelease + _ptrManagedAssemblyLoadContextOffset);
            var hostAssemblyLoadContext = _dataTarget.DataReader.Read<ulong>(ptrManagedAssemblyLoadContext);
            var alcObject = Objects.SingleOrDefault(o => o.Address == hostAssemblyLoadContext);

            // The name of the AssemblyLoadContext is retrieved by reading the "_name" field value
            var name = alcObject.ReadStringField(_nameField);
            return name;
        }

        /// <summary>
        /// Experimental, may crash
        /// </summary>
        public string GetAllocatorNameForObject(ClrObject clrObject)
        {
            var allocatorObject = GetAllocatorObject(clrObject);
            return GetAllocatorName(allocatorObject);
        }
*/
    }
}
