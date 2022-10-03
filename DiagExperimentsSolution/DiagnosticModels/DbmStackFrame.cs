using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Diagnostics.Runtime;

namespace DiagnosticModels;

public class DbmStackFrame
{
    public ClrThread? Thread { get; set; }
    public IEnumerable<ClrStackFrame> StackFrames { get; set; }  = Enumerable.Empty<ClrStackFrame>();
}
