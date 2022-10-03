using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Diagnostics.Runtime;

namespace DiagnosticModels;

public class DbmStringsBySize
{
    public ClrObject Obj { get; set; }
    public string Text { get; set; } = string.Empty;
    public long Size => Text.Length;
}
