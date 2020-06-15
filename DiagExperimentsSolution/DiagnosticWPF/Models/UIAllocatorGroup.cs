using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Diagnostics.Runtime;

namespace DiagnosticWPF.Models
{
    public class UIAllocatorGroup
    {
        public ClrObject Allocator { get; set; }
        public IEnumerable<ClrObject> Objects { get; set; }
        public string Name { get; set; }
    }
}
