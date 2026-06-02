using Microsoft.Diagnostics.Runtime;

namespace DiagnosticModels.Tests;

public class DbmStringsBySizeTests
{
    [Fact]
    public void Size_ReturnsTextLength()
    {
        var sut = new DbmStringsBySize { Text = "hello" };
        sut.Size.Should().Be(5);
    }

    [Fact]
    public void Size_ReturnsZero_ForEmptyString()
    {
        var sut = new DbmStringsBySize { Text = string.Empty };
        sut.Size.Should().Be(0);
    }

    [Fact]
    public void Size_ReturnsZero_ForDefault()
    {
        var sut = new DbmStringsBySize();
        sut.Size.Should().Be(0);
    }

    [Fact]
    public void Size_ReturnsLength_ForLongString()
    {
        var sut = new DbmStringsBySize { Text = new string('x', 10000) };
        sut.Size.Should().Be(10000);
    }

    [Fact]
    public void Obj_CanBeNull()
    {
        var sut = new DbmStringsBySize { Text = "test" };
        sut.Obj.IsNull.Should().BeTrue();
    }
}
