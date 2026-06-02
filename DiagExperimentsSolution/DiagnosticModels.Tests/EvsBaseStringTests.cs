namespace DiagnosticModels.Tests;

public class EvsBaseStringTests
{
    [Fact]
    public void Constructor_StoresValue()
    {
        var sut = new EvsException("Test exception message");
        sut.Val.Should().Be("Test exception message");
    }

    [Fact]
    public void Val_ReturnsStoredString()
    {
        var sut = new EvsException("hello world");
        sut.Val.Should().Be("hello world");
    }

    [Fact]
    public void Val_ReturnsNull_WhenConstructedWithNull()
    {
        var sut = new EvsException(null!);
        sut.Val.Should().BeNull();
    }

    [Fact]
    public void Val_ReturnsEmpty_WhenConstructedWithEmpty()
    {
        var sut = new EvsException(string.Empty);
        sut.Val.Should().BeEmpty();
    }

    [Fact]
    public void EvsException_HasCorrectCategory()
    {
        var sut = new EvsException("error");
        sut.Cat.Should().Be("Last first-chance Exception");
        sut.Uom.Should().BeEmpty();
    }
}
