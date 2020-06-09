using Microsoft.Diagnostics.Runtime;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ClrDiagnostics.Extensions
{
    public static class ClrHeapExtensions
    {
        public static IEnumerable<ulong> EnumerateObjectAddresses(this ClrHeap heap,
            Func<ClrType, bool> predicate)
        {
            return heap.Segments.SelectMany(s => s.EnumerateObjectAddresses(predicate));
        }


    }
}
