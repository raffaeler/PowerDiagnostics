namespace DiagnosticModels;

/// <summary>
/// Represents an AssemblyLoadContext with its address and resolved name.
/// Used to identify which ALC loaded a given type — critical for ALC leak diagnosis.
/// </summary>
public class DbmAssemblyLoadContext
{
    /// <summary>Hex address of the ALC object on the GC heap (e.g., "00007FFA12340000").</summary>
    public string? Address { get; set; }

    /// <summary>
    /// Human-readable name of the ALC.
    /// "Default" for the process-wide default ALC, or a custom name for collectible ALCs.
    /// Null for old CLRs where ALC info is unavailable.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// True when this is the default process-wide AssemblyLoadContext (Name == "Default").
    /// </summary>
    public bool IsDefault => Name == "Default";
}
