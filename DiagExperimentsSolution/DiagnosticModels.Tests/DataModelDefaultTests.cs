using Microsoft.Diagnostics.Runtime;

namespace DiagnosticModels.Tests;

public class DataModelDefaultTests
{
    [Fact]
    public void DbmAllocatorGroup_Objects_DefaultsToEmpty()
    {
        var sut = new DbmAllocatorGroup();
        sut.Objects.Should().BeEmpty();
    }

    [Fact]
    public void DbmAllocatorGroup_Name_DefaultsToEmpty()
    {
        var sut = new DbmAllocatorGroup();
        sut.Name.Should().BeEmpty();
    }

    [Fact]
    public void DbmDumpHeapStat_Objects_IsInitialized()
    {
        var sut = new DbmDumpHeapStat();
        sut.Objects.Should().NotBeNull();
        sut.Objects.Should().BeEmpty();
    }

    [Fact]
    public void DbmDumpHeapStat_Type_DefaultsToNull()
    {
        var sut = new DbmDumpHeapStat();
        sut.Type.Should().BeNull();
    }

    [Fact]
    public void DbmDupStrings_Text_DefaultsToEmpty()
    {
        var sut = new DbmDupStrings();
        sut.Text.Should().BeEmpty();
    }

    [Fact]
    public void DbmDupStrings_Count_DefaultsToZero()
    {
        var sut = new DbmDupStrings();
        sut.Count.Should().Be(0);
    }

    [Fact]
    public void DbmStackFrame_StackFrames_DefaultsToEmpty()
    {
        var sut = new DbmStackFrame();
        sut.StackFrames.Should().BeEmpty();
    }

    [Fact]
    public void DbmStackFrame_Thread_DefaultsToNull()
    {
        var sut = new DbmStackFrame();
        sut.Thread.Should().BeNull();
    }

    [Fact]
    public void DbmStaticFields_Obj_DefaultsToNull()
    {
        var sut = new DbmStaticFields();
        sut.Obj.IsNull.Should().BeTrue();
    }

    [Fact]
    public void DbmStaticFields_Size_DefaultsToZero()
    {
        var sut = new DbmStaticFields();
        sut.Size.Should().Be(0);
    }
}
