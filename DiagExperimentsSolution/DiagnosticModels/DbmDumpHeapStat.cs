using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Diagnostics.Runtime;

namespace DiagnosticModels;

public class DbmDumpHeapStat
{
    public ClrType? Type { get; set; }
    public List<ClrObject> Objects { get; set; } = new List<ClrObject>();
    public long GraphSize { get; set; }
}
