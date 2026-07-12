using DiagnosticModels;

using Microsoft.Diagnostics.Runtime;

namespace DiagnosticInvestigations.Tests;

public class KnownQueryMetadataTests
{
    private readonly QueriesService _queriesService = new();

    [Fact]
    public void AllQueries_HaveMetadata()
    {
        foreach (var (name, query) in _queriesService.Queries)
        {
            var metadata = query.GetMetadata();

            metadata.Should().NotBeNull($"query '{name}' should return metadata");
            metadata.QueryName.Should().Be(name);
            metadata.Columns.Should().NotBeEmpty($"query '{name}' should have master columns");
        }
    }

    [Theory]
    [InlineData("DumpHeapStat", true, "Objects")]
    [InlineData("GetStaticFieldsWithGraphAndSize", true, "Obj")]
    [InlineData("GetDuplicateStrings", false, null)]
    [InlineData("GetStringsBySize", false, null)]
    [InlineData("Modules", true, null)]
    [InlineData("Threads stacks", true, "StackFrames")]
    [InlineData("Roots", false, null)]
    [InlineData("ObjectsBySize", false, null)]
    [InlineData("NonSystemObjectsBySize", false, null)]
    [InlineData("GetObjectsGroupedByAllocator (.NET5+ dumps)", true, "Objects")]
    public void Query_HasCorrectDetailsMetadata(string queryName, bool expectedHasDetails, string? expectedDetailProperty)
    {
        var query = _queriesService.Queries[queryName];
        query.Should().NotBeNull();

        query.HasDetails.Should().Be(expectedHasDetails);
        query.DetailProperty.Should().Be(expectedDetailProperty);

        if (expectedHasDetails)
        {
            query.DetailType.Should().NotBeNull($"{queryName} should have DetailType");
            if (queryName == "Threads stacks")
                query.DetailType.Should().Be(typeof(ClrStackFrame));
            else if (queryName == "Modules")
                query.DetailType.Should().Be(typeof(DiagnosticModels.ModuleDataDetail));
            else
                query.DetailType.Should().Be(typeof(ClrObject));
        }
        else
        {
            query.DetailType.Should().BeNull();
        }
    }

    [Fact]
    public void DumpHeapStat_Metadata_HasCorrectColumns()
    {
        var meta = _queriesService.Queries["DumpHeapStat"].GetMetadata();

        meta.Columns.Should().HaveCount(3);
        meta.Columns[0].Header.Should().Be("Type");
        meta.Columns[1].Header.Should().Be("MT");
        meta.Columns[2].Header.Should().Be("Graph Size");
        meta.DetailColumns.Should().HaveCount(3); // Address, Size, Type
    }

    [Fact]
    public void ThreadsStacks_Metadata_HasCorrectColumns()
    {
        var meta = _queriesService.Queries["Threads stacks"].GetMetadata();

        meta.Columns.Should().HaveCount(3);
        meta.Columns[0].Header.Should().Be("IsAlive");
        meta.DetailColumns.Should().HaveCount(4); // FrameName, Method, Kind, StackPointer
        meta.DetailColumns[0].Header.Should().Be("FrameName");
    }

    [Fact]
    public void AllocatorGroup_Metadata_HasCorrectColumns()
    {
        var meta = _queriesService.Queries["GetObjectsGroupedByAllocator (.NET5+ dumps)"].GetMetadata();

        meta.Columns.Should().HaveCount(4);
        meta.Columns[0].Header.Should().Be("Allocator Address");
        meta.DetailColumns.Should().HaveCount(3); // Address, Size, Type
    }
}
