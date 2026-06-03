using Microsoft.Diagnostics.Runtime;

namespace DiagnosticModels;

/// <summary>
/// Result returned by the MethodTable endpoint: all heap objects for a specific MethodTable.
/// </summary>
public class MethodTableResult
{
    /// <summary>MethodTable address as a hex string (e.g., "0x00007FFF12345678").</summary>
    public string Mt { get; set; } = string.Empty;

    /// <summary>CLR type name for this MethodTable.</summary>
    public string? TypeName { get; set; }

    /// <summary>Total graph size of all objects with this MT.</summary>
    public long GraphSize { get; set; }

    /// <summary>Number of objects with this MT.</summary>
    public int ObjectCount { get; set; }

    /// <summary>All heap objects belonging to this MethodTable.</summary>
    public List<ClrObject> Objects { get; set; } = new();
}
