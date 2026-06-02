using Microsoft.Diagnostics.Runtime;

using System;
using System.Collections.Generic;
using System.Text;

namespace ClrDiagnostics.Extensions
{
    public static class ClrSegmentExtensions
    {
        /// <summary>
        /// Enumerate object addresses in a segment matching the given predicate.
        /// v3: <c>segment.Heap</c> was removed — pass the <see cref="ClrHeap"/> explicitly.
        /// v3: <c>segment.GetNextObjectAddress</c> was removed — use <see cref="ClrHeap.FindNextObjectOnSegment"/>.
        /// </summary>
        public static IEnumerable<ulong> EnumerateObjectAddresses(this ClrSegment segment,
            ClrHeap heap, Func<ClrType, bool> predicate)
        {
            var address = segment.FirstObjectAddress;
            while (address != 0 && address < segment.CommittedMemory.End)
            {
                if (predicate != null)
                {
                    var type = heap.GetObjectType(address);
                    if (predicate(type)) yield return address;
                }
                else
                {
                    yield return address;
                }

                address = heap.FindNextObjectOnSegment(address);
            }
        }


    }
}
