using DiagnosticModels;

using Microsoft.Diagnostics.Runtime;

namespace DiagnosticInvestigations;

/// <summary>
/// Populates QueryMetadata with column definitions for each query type.
/// Mirrors the WPF KnownGrids registry from the WPF-Functionality-Reference §5.
/// All addresses use hex format (0:X16), sizes use comma format (0:N0).
/// </summary>
public static class QueryMetadataFactory
{
    public static void PopulateMetadata(QueryMetadata metadata)
    {
        switch (metadata.ResultType)
        {
            case var t when t.Contains(nameof(DbmDumpHeapStat)):
                PopulateDbmDumpHeapStat(metadata);
                break;
            case var t when t.Contains(nameof(DbmStaticFields)):
                PopulateDbmStaticFields(metadata);
                break;
            case var t when t.Contains(nameof(DbmDupStrings)):
                PopulateDbmDupStrings(metadata);
                break;
            case var t when t.Contains(nameof(DbmStringsBySize)):
                PopulateDbmStringsBySize(metadata);
                break;
            case var t when t.Contains(nameof(ClrModule)):
                PopulateClrModule(metadata);
                break;
            case var t when t.Contains(nameof(DbmStackFrame)):
                PopulateDbmStackFrame(metadata);
                break;
            case var t when t.Contains(nameof(ClrRoot)):
                PopulateClrRoot(metadata);
                break;
            case var t when t.Contains(nameof(ClrObject)):
                PopulateClrObject(metadata);
                break;
            case var t when t.Contains(nameof(DbmAllocatorGroup)):
                PopulateDbmAllocatorGroup(metadata);
                break;
        }
    }

    // §5.1 DbmDumpHeapStat → ClrObject details
    private static void PopulateDbmDumpHeapStat(QueryMetadata m)
    {
        m.Columns = new List<ColumnDefinition>
        {
            new() { Header = "Type", Path = "TypeName", Tooltip = "Type" },
            new() { Header = "MT", Path = "MT", Format = "0:X16", Tooltip = "MethodTable" },
            new() { Header = "Graph Size", Path = "GraphSize", Format = "0:N0", AlignRight = true, Tooltip = "GraphSize" },
        };
        m.DetailColumns = ClrObjectDetailColumns();
    }

    // §5.4 DbmStaticFields → ClrObject details
    private static void PopulateDbmStaticFields(QueryMetadata m)
    {
        m.Columns = new List<ColumnDefinition>
        {
            new() { Header = "Static field name", Path = "Field.Name", Tooltip = "Field.Name" },
            new() { Header = "Size", Path = "Size", Format = "0:N0", AlignRight = true, Tooltip = "Size" },
            new() { Header = "Object", Path = "Obj", Tooltip = "Obj" },
        };
        m.DetailColumns = ClrObjectDetailColumns();
    }

    // §5.5 DbmDupStrings — no details
    private static void PopulateDbmDupStrings(QueryMetadata m)
    {
        m.Columns = new List<ColumnDefinition>
        {
            new() { Header = "String", Path = "Text", Tooltip = "Text" },
            new() { Header = "Count", Path = "Count", Format = "0:N0", AlignRight = true, Tooltip = "Count" },
        };
    }

    // §5.6 DbmStringsBySize — no details
    private static void PopulateDbmStringsBySize(QueryMetadata m)
    {
        m.Columns = new List<ColumnDefinition>
        {
            new() { Header = "Object", Path = "Obj", Tooltip = "Obj" },
            new() { Header = "String", Path = "Text", Tooltip = "Text" },
            new() { Header = "Size", Path = "Size", Format = "0:N0", AlignRight = true, Tooltip = "Size" },
        };
    }

    // §5.7 ClrModule — no details
    private static void PopulateClrModule(QueryMetadata m)
    {
        m.Columns = new List<ColumnDefinition>
        {
            new() { Header = "AssemblyName", Path = "AssemblyName", Tooltip = "AssemblyName" },
            new() { Header = "Name", Path = "Name", Tooltip = "Name" },
            new() { Header = "Address", Path = "Address", Format = "0:X16", AlignRight = true, Tooltip = "Address" },
            new() { Header = "Size", Path = "Size", Format = "0:N0", AlignRight = true, Tooltip = "Size" },
        };
    }

    // §5.8–5.9 DbmStackFrame → ClrStackFrame details
    private static void PopulateDbmStackFrame(QueryMetadata m)
    {
        m.Columns = new List<ColumnDefinition>
        {
            new() { Header = "IsAlive", Path = "Thread.IsAlive", Tooltip = "Thread.IsAlive" },
            new() { Header = "ManagedThreadId", Path = "Thread.ManagedThreadId", Tooltip = "Thread.ManagedThreadId" },
            new() { Header = "Address", Path = "Thread.Address", Format = "0:X16", AlignRight = true, Tooltip = "Thread.Address" },
        };
        m.DetailColumns = new List<ColumnDefinition>
        {
            new() { Header = "FrameName", Path = "FrameName", Tooltip = "FrameName" },
            new() { Header = "Method", Path = "Method", Tooltip = "Method" },
            new() { Header = "Kind", Path = "Kind", Tooltip = "Kind" },
            new() { Header = "StackPointer", Path = "StackPointer", Format = "0:X16", AlignRight = true, Tooltip = "StackPointer" },
        };
    }

    // §5.10 ClrRoot — no details
    private static void PopulateClrRoot(QueryMetadata m)
    {
        m.Columns = new List<ColumnDefinition>
        {
            new() { Header = "Address", Path = "Address", Format = "0:X16", AlignRight = true, Tooltip = "Address" },
            new() { Header = "Object", Path = "Object", Tooltip = "Object" },
            new() { Header = "IsPinned", Path = "IsPinned", Tooltip = "IsPinned" },
        };
    }

    // §5.2–5.3 ClrObject (standalone, used by ObjectsBySize / NonSystemObjectsBySize) — no details
    private static void PopulateClrObject(QueryMetadata m)
    {
        m.Columns = ClrObjectDetailColumns();
    }

    // §5.11 DbmAllocatorGroup → ClrObject details
    private static void PopulateDbmAllocatorGroup(QueryMetadata m)
    {
        m.Columns = new List<ColumnDefinition>
        {
            new() { Header = "Allocator Address", Path = "Allocator.Address", Format = "0:X16", AlignRight = true, Tooltip = "Allocator.Address" },
            new() { Header = "Allocator Size", Path = "Allocator.Size", Format = "0:N0", AlignRight = true, Tooltip = "Allocator.Size" },
            new() { Header = "Allocator Type", Path = "Allocator.Type", Tooltip = "Allocator.Type" },
            new() { Header = "Allocator Name", Path = "Name", Tooltip = "Name" },
        };
        m.DetailColumns = ClrObjectDetailColumns();
    }

    /// <summary>Standard ClrObject columns used in detail grids (Address, Size, Type).</summary>
    private static List<ColumnDefinition> ClrObjectDetailColumns() => new()
    {
        new() { Header = "Address", Path = "Address", Format = "0:X16", AlignRight = true, Tooltip = "Address" },
        new() { Header = "Size", Path = "Size", Format = "0:N0", AlignRight = true, Tooltip = "Size" },
        new() { Header = "Type", Path = "Type", Tooltip = "Type" },
    };
}
