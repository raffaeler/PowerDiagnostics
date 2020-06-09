using Microsoft.Diagnostics.Runtime;

using System;
using System.Collections.Generic;
using System.Text;

namespace ClrDiagnostics.Extensions
{
    public static class ClrSegmentExtensions
    {
        public static IEnumerable<ulong> EnumerateObjectAddresses(this ClrSegment segment,
            Func<ClrType, bool> predicate)
        {
            var address = segment.FirstObjectAddress;
            while (address != 0 && address < segment.CommittedMemory.End)
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
