using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Diagnostics.Runtime;

namespace DiagnosticModels;

public class DbmStaticFields
{
    public ClrStaticField? Field { get; set; }
    public ClrObject Obj { get; set; }
    public long Size { get; set; }

    /// <summary>AssemblyLoadContext that contains this object's type, resolved with name.</summary>
    public DbmAssemblyLoadContext? AssemblyLoadContext { get; set; }
}
