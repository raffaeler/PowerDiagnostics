using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.Diagnostics.Runtime;

namespace TestConsole.Helpers
{
    public static class ClrMDExtensions
    {
        /// <summary>
        /// Note: https://github.com/microsoft/clrmd/issues/567#issuecomment-601314348
        /// </summary>
        /// <param name="runtime"></param>
        /// <param name="predicate"></param>
        /// <returns></returns>
        public static IEnumerable<ClrType> GetConstructedTypeDefinitions(this ClrRuntime runtime,
            Func<ClrType, bool> predicate)
        {
            return runtime.EnumerateModules()
                .SelectMany(m => m.EnumerateTypeDefToMethodTableMap())
                .Select(t => runtime.GetTypeByMethodTable(t.MethodTable))
                .Where(predicate);
        }

        public static IEnumerable<ulong> EnumerateObjectAddresses(this ClrHeap heap, Func<ClrType, bool> predicate)
        {
            return heap.Segments.SelectMany(s => EnumerateObjectAddresses(s, predicate));
        }

        public static IEnumerable<ulong> EnumerateObjectAddresses(this ClrSegment segment,
            Func<ClrType, bool> predicate)
        {
            var address = segment.FirstObjectAddress;
            while(address != 0 && address < segment.CommittedMemory.End)
            {
                if (predicate != null)
                {
                    var type = segment.Heap.GetObjectType(address);
                    if (predicate(type)) yield return address;
                }
                else
                {
                    yield return address;
                }

                address = segment.GetNextObjectAddress(address);
            }
        }


    }
}
