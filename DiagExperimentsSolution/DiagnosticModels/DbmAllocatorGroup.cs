using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Diagnostics.Runtime;

namespace DiagnosticModels
{
    public class DbmAllocatorGroup
    {
        public ClrObject Allocator { get; set; }
        public IEnumerable<ClrObject> Objects { get; set; } = Enumerable.Empty<ClrObject>();
        public string Name { get; set; } = string.Empty;
    }
}
