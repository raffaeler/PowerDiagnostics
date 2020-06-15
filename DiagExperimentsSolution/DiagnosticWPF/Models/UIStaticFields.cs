using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Diagnostics.Runtime;

namespace DiagnosticWPF.Models
{
    public class UIStaticFields
    {
        // (ClrStaticField field, ClrObject obj)
        public ClrStaticField Field { get; set; }
        public ClrObject Obj { get; set; }
        public long Size { get; set; }
    }
}
