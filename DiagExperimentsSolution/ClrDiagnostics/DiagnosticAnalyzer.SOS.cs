using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.Diagnostics.Runtime;
using ClrDiagnostics.Extensions;
using ClrDiagnostics.Models;

namespace ClrDiagnostics
{
    /// <summary>
    /// This source file contains the methods emulating the SOS commands
    /// </summary>
    public partial class DiagnosticAnalyzer
    {
        /// <summary>
        /// equivalent to SOS dumpheap -stat
        /// </summary>
        public IEnumerable<(ClrType type, List<ClrObject> objects, long size)> DumpHeapStat(
            long minTotalSize = 1024)
        {
            return Objects
                .GroupBy(o => o.Type, o => o)
                .Select(o => (type: o.Key, objects: o.ToList(), totalSize: o.Sum(s => (long)s.Size)))
                .Where(t => t.totalSize > minTotalSize)
                .OrderBy(t => t.totalSize);
        }

    }
}
