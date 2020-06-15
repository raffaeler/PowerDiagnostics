using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Diagnostics.Runtime;

namespace DiagnosticWPF.Models
{
    public class UIStackFrame
    {
        public ClrThread Thread { get; set; }
        public IEnumerable<ClrStackFrame> StackFrames { get; set; }
    }
}
