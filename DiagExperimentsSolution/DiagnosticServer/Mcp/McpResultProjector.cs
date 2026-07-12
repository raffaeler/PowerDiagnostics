using Microsoft.Diagnostics.Runtime;

using DiagnosticModels;

namespace DiagnosticServer.Mcp;

/// <summary>
/// Projects ClrMD types to minimal anonymous objects suitable for AI consumption.
/// Unlike full serialization through converters, these projections only include
/// the fields an AI agent actually needs — reducing payload size from 20KB+ per page
/// to a few hundred bytes.
/// </summary>
internal static class McpResultProjector
{
    /// <summary>
    /// Projects any known ClrMD / DBM type to a minimal JSON-safe object.
    /// Unknown types are returned as-is.
    /// </summary>
    public static object? Project(object? row)
    {
        if (row is null) return null;

        return row switch
        {
            ClrModule m => CompactModule(m),
            ModuleDataLight md => CompactModuleDataLight(md),
            ModuleDataDetail dd => CompactModuleDataDetail(dd),
            ClrRoot r => CompactRoot(r),
            ClrStackFrame sf => CompactStackFrame(sf),
            ClrThread t => CompactThread(t),
            ClrType type => CompactType(type),
            ClrInstanceField f => CompactInstanceField(f),
            ClrStaticField sf2 => CompactStaticField(sf2),
            ClrMethod method => CompactMethod(method),
            GcReferenceInfo rref => CompactGcReference(rref),
            GcRootPathNode node => CompactGcRootNode(node),

            DbmDumpHeapStat hs => CompactHeapStat(hs),
            DbmStaticFields sf => CompactStaticFieldsEntry(sf),
            DbmDupStrings ds => new { text = ds.Text, count = ds.Count },
            DbmStringsBySize ss => CompactStringsBySize(ss),
            DbmStackFrame tsf => CompactThreadStack(tsf),
            DbmAllocatorGroup ag => new
            {
                name = ag.Name,
                objectCount = ag.Objects?.Count() ?? 0,
            },

            // ClrObject is a ref struct — handle specially
            object o when o.GetType() == typeof(ClrObject) => CompactClrObject((ClrObject)o),

            string or byte or sbyte or short or ushort or int or uint
                or long or ulong or float or double or bool or decimal => row,

            _ => new { type = row.GetType().Name, value = Truncate(row.ToString() ?? "", 200) },
        };
    }

    // ──────── Compact projections ────────

    private static object CompactClrObject(ClrObject o) => new
    {
        address = $"0x{o.Address:X}",
        size = o.Size,
        typeName = o.Type?.Name ?? "(unknown)",
        alcAddress = o.Type?.AssemblyLoadContextAddress is ulong alc && alc != 0 ? $"0x{alc:X}" : null,
    };

    private static object CompactModule(ClrModule m) => new
    {
        name = m.Name,
        imageBase = $"0x{m.ImageBase:X}",
        size = m.Size,
        isDynamic = m.IsDynamic,
    };

    private static object CompactModuleDataLight(ModuleDataLight md) => new
    {
        assemblyName = md.AssemblyName,
        name = md.Name,
        address = md.Address,
        size = md.Size,
        isDynamic = md.IsDynamic,
        isNative = md.IsNative,
        fileName = md.FileName,
    };

    private static object CompactModuleDataDetail(ModuleDataDetail dd) => new
    {
        assemblyName = dd.AssemblyName,
        name = dd.Name,
        address = dd.Address,
        size = dd.Size,
        isDynamic = dd.IsDynamic,
        isNative = dd.IsNative,
        isManaged = dd.IsManaged,
        isExtracted = dd.IsExtracted,
        pdbName = dd.PdbName,
        pdbGuid = dd.PdbGuid,
        pdbAge = dd.PdbAge,
        hasPdb = dd.HasPdb,
        architecture = dd.TargetArchitecture,
        moduleVersion = dd.ModuleVersion,
        indexFileSize = dd.IndexFileSize,
        indexTimeStamp = dd.IndexTimeStamp,
    };

    private static object CompactRoot(ClrRoot r) => new
    {
        address = $"0x{r.Address:X}",
        rootKind = r.RootKind.ToString(),
        objectAddress = $"0x{r.Object.Address:X}",
        objectTypeName = r.Object.Type?.Name ?? "(unknown)",
        objectSize = r.Object.Size,
        alcAddress = r.Object.Type?.AssemblyLoadContextAddress is ulong alc && alc != 0 ? $"0x{alc:X}" : null,
    };

    private static object CompactStackFrame(ClrStackFrame sf) => new
    {
        methodName = sf.Method?.Name ?? "(unknown)",
        offset = sf.InstructionPointer == 0 ? null : $"0x{sf.InstructionPointer:X}",
        moduleName = sf.Method?.Type?.Module?.Name,
    };

    private static object CompactThread(ClrThread t) => new
    {
        osThreadId = t.OSThreadId,
        address = $"0x{t.Address:X}",
        isAlive = t.IsAlive,
    };

    private static object CompactType(ClrType t) => new
    {
        name = t.Name,
        methodTable = $"0x{t.MethodTable:X}",
        isFree = t.IsFree,
        isArray = t.IsArray,
        alcAddress = t.AssemblyLoadContextAddress != 0 ? $"0x{t.AssemblyLoadContextAddress:X}" : null,
    };

    private static object CompactInstanceField(ClrInstanceField f) => new
    {
        name = f.Name,
        typeName = f.Type?.Name,
        offset = f.Offset,
    };

    private static object CompactStaticField(ClrStaticField sf) => new
    {
        name = sf.Name,
        typeName = sf.Type?.Name,
    };

    private static object CompactMethod(ClrMethod m) => new
    {
        name = m.Name,
        signature = m.Signature,
    };

    private static object CompactGcReference(GcReferenceInfo r) => new
    {
        address = $"0x{r.Address:X}",
        typeName = r.TypeName,
        fieldName = r.FieldName,
    };

    private static object CompactGcRootNode(GcRootPathNode node) => new
    {
        objectAddress = $"0x{node.ObjectAddress:X}",
        typeName = node.TypeName,
        rootKind = node.RootKind?.ToString(),
    };

    // ──────── DBM projections ────────

    private static object CompactHeapStat(DbmDumpHeapStat hs) => new
    {
        typeName = hs.TypeName,
        mt = hs.MT > 0 ? $"0x{hs.MT:X}" : null,
        objectCount = hs.Objects?.Count ?? 0,
        graphSize = hs.GraphSize,
        alcAddress = hs.Type?.AssemblyLoadContextAddress is ulong alc && alc != 0 ? $"0x{alc:X}" : null,
        sampleAddresses = hs.Objects?.Take(3).Select(o => $"0x{o.Address:X}").ToList(),
    };

    private static object CompactStaticFieldsEntry(DbmStaticFields sf) => new
    {
        field = sf.Field?.Name,
        fieldType = sf.Field?.Type?.Name,
        size = sf.Size,
        objAddress = sf.Obj.Address > 0 ? $"0x{sf.Obj.Address:X}" : null,
        objTypeName = sf.Obj.Type?.Name,
        objSize = sf.Obj.Size,
        alcAddress = sf.Obj.Type?.AssemblyLoadContextAddress is ulong alc && alc != 0 ? $"0x{alc:X}" : null,
    };

    private static object CompactStringsBySize(DbmStringsBySize ss) => new
    {
        text = Truncate(ss.Text ?? "", 120),
        size = ss.Obj.Size,
        address = ss.Obj.Address > 0 ? $"0x{ss.Obj.Address:X}" : null,
        typeName = ss.Obj.Type?.Name,
        alcAddress = ss.Obj.Type?.AssemblyLoadContextAddress is ulong alc && alc != 0 ? $"0x{alc:X}" : null,
    };

    private static object CompactThreadStack(DbmStackFrame tsf)
    {
        var sfList = tsf.StackFrames?.Select(CompactStackFrame).ToList();
        return new
        {
            thread = tsf.Thread is not null ? CompactThread(tsf.Thread) : null,
            stackFrames = sfList,
            frameCount = sfList?.Count ?? 0,
        };
    }

    // ──────── Helpers ────────

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength) return value;
        return value[..(maxLength - 3)] + "...";
    }
}
