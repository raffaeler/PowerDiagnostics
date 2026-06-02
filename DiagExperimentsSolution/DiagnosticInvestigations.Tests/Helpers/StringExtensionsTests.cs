using DiagnosticInvestigations.Helpers;

namespace DiagnosticInvestigations.Tests.Helpers;

public class StringExtensionsTests
{
    [Theory]
    [InlineData("HelloWorld", "hello", true)]
    [InlineData("HelloWorld", "WORLD", true)]
    [InlineData("HelloWorld", "lowo", true)]
    [InlineData("HelloWorld", "HELLO", true)]
    [InlineData("Hello", "xyz", false)]
    [InlineData("Hello", "HELLO WORLD", false)]
    public void FilterBy_ReturnsExpectedResult(string text, string filter, bool expected)
    {
        text.FilterBy(filter).Should().Be(expected);
    }

    [Fact]
    public void FilterBy_EmptyString_DoesNotMatch()
    {
        "".FilterBy("x").Should().BeFalse();
    }

    [Fact]
    public void FilterBy_EmptyFilter_MatchesEverything()
    {
        "test".FilterBy("").Should().BeTrue();
        "".FilterBy("").Should().BeTrue();
    }

    [Fact]
    public void FilterBy_IsCaseInsensitive()
    {
        "CASE".FilterBy("case").Should().BeTrue();
        "case".FilterBy("CASE").Should().BeTrue();
        "CaSe".FilterBy("cAsE").Should().BeTrue();
    }
}
