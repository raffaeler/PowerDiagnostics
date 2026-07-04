namespace DiagnosticModels;

/// <summary>
/// Full-detail module DTO for the inline detail panel.
/// Contains all metadata useful for decompilation (ilspycmd) including PDB info,
/// but no local file-system paths.
/// </summary>
public class ModuleDataDetail
{
    // ── Inherited from ModuleDataLight ──

    /// <summary>Short assembly name (e.g., "MyApp").</summary>
    public string AssemblyName { get; set; } = string.Empty;

    /// <summary>Full module name (e.g., "MyApp.dll").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Image base address in hex format (e.g., "0x00007FFA12340000").</summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>Size of the module in bytes (0 if not extractable).</summary>
    public long Size { get; set; }

    /// <summary>True for dynamic assemblies with no backing PE file.</summary>
    public bool IsDynamic { get; set; }

    /// <summary>True for native (non-.NET) modules.</summary>
    public bool IsNative { get; set; }

    /// <summary>Original module filename from the dump metadata.</summary>
    public string? FileName { get; set; }

    // ── Decompilation metadata ──

    /// <summary>Image base as raw hex string (e.g., "7FFA12340000").</summary>
    public string ImageBase { get; set; } = string.Empty;

    /// <summary>PDB filename (e.g., "MyApp.pdb"), without directory path.</summary>
    public string? PdbName { get; set; }

    /// <summary>PDB GUID as a string (e.g., "a1b2c3d4-e5f6-...").</summary>
    public string? PdbGuid { get; set; }

    /// <summary>PDB age value.</summary>
    public int? PdbAge { get; set; }

    /// <summary>True if PDB metadata was found in the module.</summary>
    public bool HasPdb { get; set; }

    /// <summary>Target architecture string (e.g., "Amd64", "X86", "Arm64").</summary>
    public string? TargetArchitecture { get; set; }

    /// <summary>True for managed (.NET) modules.</summary>
    public bool IsManaged { get; set; }

    /// <summary>
    /// Module product/file version if discoverable (e.g., "1.2.3.0").
    /// </summary>
    public string? ModuleVersion { get; set; }

    /// <summary>Size of the PE file on disk as recorded in the dump index.</summary>
    public long IndexFileSize { get; set; }

    /// <summary>PE timestamp from the dump index.</summary>
    public uint IndexTimeStamp { get; set; }

    /// <summary>True if the module file was successfully extracted to disk.</summary>
    public bool IsExtracted { get; set; }
}
