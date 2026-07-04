using Microsoft.Diagnostics.Runtime;

namespace ClrDiagnostics.Models;

/// <summary>
/// Represents a module extracted from a memory dump and saved to a local subfolder.
/// </summary>
public class ModuleData
{
    /// <summary>
    /// Relative path to the extracted module file, including the "modules" subfolder
    /// (e.g., "modules/MyApp.dll").
    /// </summary>
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>
    /// <see langword="true"/> if this is a native (non-.NET) module;
    /// <see langword="false"/> for managed assemblies.
    /// </summary>
    public bool IsNative { get; set; }

    /// <summary>
    /// The managed <see cref="ClrModule"/> if <see cref="IsNative"/> is <see langword="false"/>;
    /// otherwise <see langword="null"/>.
    /// </summary>
    public ClrModule? Module { get; set; }

    /// <summary>
    /// Short assembly name for managed modules, or filename stem for native modules.
    /// </summary>
    public string? AssemblyName { get; set; }

    /// <summary>
    /// Size in bytes of the extracted file on disk. 0 if the file was not extracted
    /// (e.g., dynamic assemblies or modules with no PE backing).
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// <see langword="true"/> for dynamic (reflection-emit) assemblies that have no backing PE file.
    /// Always <see langword="false"/> for native modules.
    /// </summary>
    public bool IsDynamic { get; set; }

    /// <summary>
    /// Original module filename from the dump metadata (e.g., "C:\Windows\System32\ntdll.dll").
    /// </summary>
    public string? FileName { get; set; }
}
