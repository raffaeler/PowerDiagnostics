using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

using Microsoft.Diagnostics.Runtime;
using ClrDiagnostics.Extensions;
using ClrDiagnostics.Models;

namespace ClrDiagnostics;

public partial class DiagnosticAnalyzer
{
    /// <summary>
    /// Extracts all modules from the dump into a "modules" subfolder under the dump directory.
    /// Returns metadata for each extracted (or skipped) module.
    /// </summary>
    /// <param name="includeNativeModules">
    /// When <see langword="true"/>, native DLLs (ntdll.dll, kernel32.dll, etc.) are also extracted.
    /// Defaults to <see langword="false"/> — only managed assemblies are extracted.
    /// </param>
    /// <returns>A read-only list of <see cref="ModuleData"/> for every module found.</returns>
    public IReadOnlyList<Models.ModuleData> ExtractModules(bool includeNativeModules = false)
    {
        // Create the "modules" subfolder under the dump directory
        var modulesDir = new DirectoryInfo(Path.Combine(_dumpDirectory.FullName, "modules"));
        if (!modulesDir.Exists)
            modulesDir.Create();

        // Build a lookup of ClrModule by ImageBase for cross-referencing
        var clrModuleByImageBase = new Dictionary<ulong, ClrModule>();
        foreach (var clrModule in _clrRuntime.EnumerateModules())
        {
            if (clrModule.ImageBase != 0)
                clrModuleByImageBase[clrModule.ImageBase] = clrModule;
        }

        var result = new List<Models.ModuleData>();
        var usedFileNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (ModuleInfo dataModule in _dataTarget.EnumerateModules())
        {
            bool isManaged = dataModule.IsManaged;

            // Filter: skip native modules unless explicitly included
            if (!includeNativeModules && !isManaged)
                continue;

            // Cross-reference with ClrModule by ImageBase
            clrModuleByImageBase.TryGetValue(dataModule.ImageBase, out var clrModule);

            // Determine the original filename for this module
            string? sourceFileName = dataModule.FileName;
            if (string.IsNullOrEmpty(sourceFileName) && clrModule != null)
                sourceFileName = clrModule.Name;
            if (string.IsNullOrEmpty(sourceFileName))
                sourceFileName = $"module_0x{dataModule.ImageBase:X}.dll";

            // Sanitize and deduplicate output filename
            string safeFileName = SanitizeFileName(Path.GetFileName(sourceFileName));
            safeFileName = DeduplicateFileName(usedFileNames, safeFileName);

            string relativePath = Path.Combine("modules", safeFileName);
            string absolutePath = Path.Combine(modulesDir.FullName, safeFileName);
            long fileSize = 0;

            // Decide whether this module can be extracted
            bool isDynamic = clrModule?.IsDynamic ?? false;
            bool isPEFile = clrModule?.IsPEFile ?? true; // native modules are PE-backed

            if (!isDynamic && isPEFile && dataModule.IndexFileSize > 0)
            {
                try
                {
                    byte[] buffer = new byte[dataModule.IndexFileSize];
                    int bytesRead = _dataTarget.DataReader.Read(dataModule.ImageBase, buffer);
                    if (bytesRead > 0)
                    {
                        File.WriteAllBytes(absolutePath, buffer);
                        fileSize = new FileInfo(absolutePath).Length;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to extract module '{sourceFileName}': {ex.Message}");
                }
            }

            string assemblyName = GetFullAssemblyName(clrModule)
                ?? clrModule?.AssemblyName
                ?? Path.GetFileNameWithoutExtension(sourceFileName);

            result.Add(new Models.ModuleData
            {
                RelativePath = relativePath,
                IsNative = !isManaged,
                Module = clrModule,
                AssemblyName = assemblyName,
                FileSize = fileSize,
                IsDynamic = isDynamic,
                FileName = sourceFileName,
            });
        }

        return result;
    }

    /// <summary>
    /// Replaces characters that are invalid in file names with underscores.
    /// </summary>
    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = fileName.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (invalid.Contains(chars[i]))
                chars[i] = '_';
        }
        return new string(chars);
    }

    /// <summary>
    /// Appends a numeric suffix ("_2", "_3", ...) to <paramref name="fileName"/> if it has
    /// already been used, and records the usage. Returns the deduplicated filename.
    /// </summary>
    private static string DeduplicateFileName(Dictionary<string, int> usedFileNames, string fileName)
    {
        if (usedFileNames.TryGetValue(fileName, out int count))
        {
            count++;
            usedFileNames[fileName] = count;

            string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            string ext = Path.GetExtension(fileName);
            return $"{nameWithoutExt}_{count}{ext}";
        }
        else
        {
            usedFileNames[fileName] = 1;
            return fileName;
        }
    }

    /// <summary>
    /// Reads the PE assembly metadata from the dump and returns the full assembly display name
    /// (e.g., "myTypes, Version=1.0.1234.0, Culture=en-US, PublicKeyToken=b77a5c561934e089").
    /// Falls back to <see langword="null"/> if the metadata cannot be read or parsed.
    /// </summary>
    private string? GetFullAssemblyName(ClrModule? module)
    {
        if (module is null) return null;
        if (module.MetadataAddress == 0 || module.MetadataLength == 0) return null;

        // Limit read size to avoid excessive memory (max 512 KB for metadata)
        int length = (int)Math.Min(module.MetadataLength, 512 * 1024);
        byte[] buffer = new byte[length];
        int bytesRead = _dataTarget.DataReader.Read(module.MetadataAddress, buffer);
        if (bytesRead <= 4) return null;

        try
        {
            unsafe
            {
                fixed (byte* pBuf = buffer)
                {
                    var reader = new System.Reflection.Metadata.MetadataReader(pBuf, bytesRead);
                    var assemblyDef = reader.GetAssemblyDefinition();

                    string name = reader.GetString(assemblyDef.Name);
                    var version = assemblyDef.Version;

                    string culture = assemblyDef.Culture.IsNil
                        ? "neutral"
                        : reader.GetString(assemblyDef.Culture);

                    // Public key token: last 8 bytes of SHA1 of the full public key
                    string publicKeyToken = "null";
                    if (!assemblyDef.PublicKey.IsNil)
                    {
                        byte[] pkBlob = reader.GetBlobBytes(assemblyDef.PublicKey);
                        byte[] hash = SHA1.HashData(pkBlob);
                        byte[] tokenBytes = hash[^8..];
                        publicKeyToken = Convert.ToHexString(tokenBytes).ToLowerInvariant();
                    }

                    // Processor architecture from AssemblyFlags bits 4-6
                    int archBits = ((int)assemblyDef.Flags & 0x70) >> 4;
                    string archStr = archBits switch
                    {
                        1 => "x86",
                        2 => "Itanium",
                        3 => "amd64",
                        4 => "arm",
                        _ => "msil",
                    };

                    return $"{name}, Version={version.Major}.{version.Minor}.{version.Build}.{version.Revision}, Culture={culture}, PublicKeyToken={publicKeyToken}, ProcessorArchitecture={archStr}";
                }
            }
        }
        catch
        {
            return null;
        }
    }
}
