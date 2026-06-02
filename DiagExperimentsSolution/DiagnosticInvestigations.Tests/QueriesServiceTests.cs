using DiagnosticInvestigations.Configurations;

namespace DiagnosticInvestigations.Tests;

public class QueriesServiceTests
{
    [Fact]
    public void Constructor_CreatesDictionary_WithTenQueries()
    {
        var sut = new QueriesService();
        sut.Queries.Count.Should().Be(10);
    }

    [Theory]
    [InlineData("DumpHeapStat", typeof(DiagnosticModels.DbmDumpHeapStat))]
    [InlineData("GetStaticFieldsWithGraphAndSize", typeof(DiagnosticModels.DbmStaticFields))]
    [InlineData("GetDuplicateStrings", typeof(DiagnosticModels.DbmDupStrings))]
    [InlineData("GetStringsBySize", typeof(DiagnosticModels.DbmStringsBySize))]
    [InlineData("Modules", typeof(Microsoft.Diagnostics.Runtime.ClrModule))]
    [InlineData("Threads stacks", typeof(DiagnosticModels.DbmStackFrame))]
    [InlineData("Roots", typeof(Microsoft.Diagnostics.Runtime.ClrRoot))]
    [InlineData("ObjectsBySize", typeof(Microsoft.Diagnostics.Runtime.ClrObject))]
    [InlineData("NonSystemObjectsBySize", typeof(Microsoft.Diagnostics.Runtime.ClrObject))]
    [InlineData("GetObjectsGroupedByAllocator (.NET5+ dumps)", typeof(DiagnosticModels.DbmAllocatorGroup))]
    public void Query_HasCorrectNameAndType(string name, Type expectedType)
    {
        var sut = new QueriesService();
        sut.Queries.Should().ContainKey(name);
        sut.Queries[name].Name.Should().Be(name);
        sut.Queries[name].Type.Should().Be(expectedType);
    }

    [Fact]
    public void DumpHeapStatQuery_HasFilterDelegate()
    {
        var sut = new QueriesService();
        var query = sut.Queries["DumpHeapStat"];

        query.Filter.Should().NotBeNull();
        query.Populate.Should().NotBeNull();
    }

    [Fact]
    public void GetDuplicateStringsQuery_HasFilterDelegate()
    {
        var sut = new QueriesService();
        var query = sut.Queries["GetDuplicateStrings"];

        query.Filter.Should().NotBeNull();
        query.Populate.Should().NotBeNull();
    }

    [Fact]
    public void NonSystemObjectsBySizeQuery_HasFilterDelegate()
    {
        var sut = new QueriesService();
        var query = sut.Queries["NonSystemObjectsBySize"];

        query.Filter.Should().NotBeNull();
        query.Populate.Should().NotBeNull();
    }

    [Fact]
    public void ModulesQuery_HasFilterDelegate()
    {
        var sut = new QueriesService();
        var query = sut.Queries["Modules"];

        query.Filter.Should().NotBeNull();
        query.Populate.Should().NotBeNull();
    }

    [Fact]
    public void AllQueries_HaveNonNullFilterAndPopulate()
    {
        var sut = new QueriesService();
        foreach (var query in sut.Queries.Values)
        {
            query.Populate.Should().NotBeNull(query.Name + " Populate");
            query.Filter.Should().NotBeNull(query.Name + " Filter");
        }
    }
}
