namespace DiagnosticModels;

/// <summary>
/// Lightweight module DTO for list display.
/// Contains no ClrMD types and no local file-system paths.
/// </summary>
public class ModuleDataLight
{
    /// <summary>Short assembly name (e.g., "MyApp").</summary>
    public string AssemblyName { get; set; } = string.Empty;

    /// <summary>Full module name (e.g., "MyApp.dll").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Image base address in hex format (e.g., "0x00007FFA12340000").</summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>Size of the module in bytes (0 if not extractable).</summary>
    public long Size { get; set; }

    /// <summary>True for dynamic (reflection-emit) assemblies with no backing PE file.</summary>
    public bool IsDynamic { get; set; }

    /// <summary>True for native (non-.NET) modules.</summary>
    public bool IsNative { get; set; }

    /// <summary>
    /// Original module filename from the dump metadata
    /// (e.g., "C:\Windows\System32\ntdll.dll").
    /// </summary>
    public string? FileName { get; set; }
}
