using System.Text.Json;

using DiagnosticModels.Converters;

namespace DiagnosticModels.Tests;

public class SerializationTests
{
    private static readonly JsonSerializerOptions Options = SetupConverters.CreateOptions();

    [Fact]
    public void GcRootPathNode_SerializesCorrectly()
    {
        var node = new GcRootPathNode
        {
            ObjectAddress = "0x000001A2B3C4D5E6",
            TypeName = "System.String",
            RootKind = "StaticVar",
            Depth = 3,
            ReferencingObjects = new List<GcReferenceInfo>
            {
                new()
                {
                    Address = "0x000001A2B3C4D5E7",
                    TypeName = "MyApp.Settings",
                    FieldName = "_config",
                    IsStatic = true,
                },
            },
        };

        var json = JsonSerializer.Serialize(node, Options);
        var deserialized = JsonSerializer.Deserialize<GcRootPathNode>(json, Options);

        deserialized.Should().NotBeNull();
        deserialized!.ObjectAddress.Should().Be("0x000001A2B3C4D5E6");
        deserialized.TypeName.Should().Be("System.String");
        deserialized.RootKind.Should().Be("StaticVar");
        deserialized.Depth.Should().Be(3);
        deserialized.ReferencingObjects.Should().HaveCount(1);
        deserialized.ReferencingObjects[0].FieldName.Should().Be("_config");
        deserialized.ReferencingObjects[0].IsStatic.Should().BeTrue();
    }

    [Fact]
    public void GcReferenceInfo_SerializesCorrectly()
    {
        var info = new GcReferenceInfo
        {
            Address = "0x000001A2B3C4D5E7",
            TypeName = "MyApp.Settings",
            FieldName = "_config",
            IsStatic = true,
        };

        var json = JsonSerializer.Serialize(info, Options);
        var deserialized = JsonSerializer.Deserialize<GcReferenceInfo>(json, Options);

        deserialized.Should().NotBeNull();
        deserialized!.Address.Should().Be("0x000001A2B3C4D5E7");
        deserialized.TypeName.Should().Be("MyApp.Settings");
        deserialized.FieldName.Should().Be("_config");
        deserialized.IsStatic.Should().BeTrue();
    }

    [Fact]
    public void GcRootPathResult_SerializesCorrectly()
    {
        var result = new GcRootPathResult
        {
            TotalPaths = 2,
            TotalReferences = 42,
            Paths = new List<GcRootPathNode>
            {
                new() { ObjectAddress = "0x100", TypeName = "Foo", Depth = 0 },
                new() { ObjectAddress = "0x200", TypeName = "Bar", Depth = 1 },
            },
        };

        var json = JsonSerializer.Serialize(result, Options);
        var deserialized = JsonSerializer.Deserialize<GcRootPathResult>(json, Options);

        deserialized.Should().NotBeNull();
        deserialized!.TotalPaths.Should().Be(2);
        deserialized.TotalReferences.Should().Be(42);
        deserialized.Paths.Should().HaveCount(2);
    }

    [Fact]
    public void HexDataResult_SerializesCorrectly()
    {
        var hex = new HexDataResult
        {
            ObjectAddress = "0x000001A2B3C4D5E6",
            TypeName = "System.Byte[]",
            Size = 256,
            BytesBase64 = "AQIDBAUG",
        };

        var json = JsonSerializer.Serialize(hex, Options);
        var deserialized = JsonSerializer.Deserialize<HexDataResult>(json, Options);

        deserialized.Should().NotBeNull();
        deserialized!.ObjectAddress.Should().Be("0x000001A2B3C4D5E6");
        deserialized.TypeName.Should().Be("System.Byte[]");
        deserialized.Size.Should().Be(256);
        deserialized.BytesBase64.Should().Be("AQIDBAUG");
    }

    [Fact]
    public void QueryResult_SerializesCorrectly()
    {
        var result = new QueryResult
        {
            QueryName = "DumpHeapStat",
            ResultType = "DiagnosticModels.DbmDumpHeapStat",
            Rows = new[] { new { Type = "System.String", Count = 5 } },
            HasDetails = true,
            DetailType = "Microsoft.Diagnostics.Runtime.ClrObject",
            DetailProperty = "Objects",
        };

        var json = JsonSerializer.Serialize(result, Options);

        json.Should().Contain("\"queryName\":\"DumpHeapStat\"");
        json.Should().Contain("\"hasDetails\":true");
        json.Should().Contain("\"detailType\":\"Microsoft.Diagnostics.Runtime.ClrObject\"");
        json.Should().Contain("\"detailProperty\":\"Objects\"");
    }

    [Fact]
    public void ColumnDefinition_SerializesCorrectly()
    {
        var col = new ColumnDefinition
        {
            Header = "Address",
            Path = "Address",
            Format = "0:X16",
            AlignRight = true,
            Tooltip = "Object Address",
        };

        var json = JsonSerializer.Serialize(col, Options);
        var deserialized = JsonSerializer.Deserialize<ColumnDefinition>(json, Options);

        deserialized.Should().NotBeNull();
        deserialized!.Header.Should().Be("Address");
        deserialized.Path.Should().Be("Address");
        deserialized.Format.Should().Be("0:X16");
        deserialized.AlignRight.Should().BeTrue();
        deserialized.Tooltip.Should().Be("Object Address");
    }

    [Fact]
    public void QueryMetadata_SerializesCorrectly()
    {
        var meta = new QueryMetadata
        {
            QueryName = "DumpHeapStat",
            ResultType = "DiagnosticModels.DbmDumpHeapStat",
            HasDetails = true,
            DetailType = "Microsoft.Diagnostics.Runtime.ClrObject",
            DetailProperty = "Objects",
            Columns = new List<ColumnDefinition>
            {
                new() { Header = "Type", Path = "Type" },
                new() { Header = "MT", Path = "Type.MethodTable", Format = "0:X16", AlignRight = true },
            },
            DetailColumns = new List<ColumnDefinition>
            {
                new() { Header = "Address", Path = "Address", Format = "0:X16", AlignRight = true },
            },
        };

        var json = JsonSerializer.Serialize(meta, Options);
        var deserialized = JsonSerializer.Deserialize<QueryMetadata>(json, Options);

        deserialized.Should().NotBeNull();
        deserialized!.QueryName.Should().Be("DumpHeapStat");
        deserialized.HasDetails.Should().BeTrue();
        deserialized.DetailProperty.Should().Be("Objects");
        deserialized.Columns.Should().HaveCount(2);
        deserialized.DetailColumns.Should().HaveCount(1);
    }

    [Fact]
    public void DbmDupStrings_SerializesCorrectly()
    {
        var dup = new DbmDupStrings
        {
            Text = "hello",
            Count = 42,
        };

        var json = JsonSerializer.Serialize(dup, Options);
        var deserialized = JsonSerializer.Deserialize<DbmDupStrings>(json, Options);

        deserialized.Should().NotBeNull();
        deserialized!.Text.Should().Be("hello");
        deserialized.Count.Should().Be(42);
    }
}
