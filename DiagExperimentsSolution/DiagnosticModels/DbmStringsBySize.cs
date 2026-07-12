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

    /// <summary>AssemblyLoadContext that contains this string's type, resolved with name.</summary>
    public DbmAssemblyLoadContext? AssemblyLoadContext { get; set; }
}
