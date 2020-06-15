using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime;

namespace DiagnosticWPF.Models
{
    public class UIDumpHeapStat
    {
        public ClrType Type { get; set; }
        public List<ClrObject> Objects { get; set; }
        public long GraphSize { get; set; }
    }
}
