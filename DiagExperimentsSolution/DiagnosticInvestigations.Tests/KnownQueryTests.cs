using ClrDiagnostics;

namespace DiagnosticInvestigations.Tests;

public class KnownQueryTests
{
    [Fact]
    public void ParameterlessConstructor_CreatesWithNullProperties()
    {
        var sut = new KnownQuery();
        sut.Type.Should().BeNull();
        sut.Name.Should().BeNull();
        sut.Populate.Should().BeNull();
        sut.Filter.Should().BeNull();
    }

    [Fact]
    public void FullConstructor_SetsAllProperties()
    {
        var populate = new Func<DiagnosticAnalyzer, System.Collections.IEnumerable>(_ => Enumerable.Empty<object>());
        var filter = new Func<object, string, bool?>((o, s) => true);

        var sut = new KnownQuery
        {
            Type = typeof(string),
            Name = "TestQuery",
            Populate = populate,
            Filter = filter,
        };

        sut.Type.Should().Be(typeof(string));
        sut.Name.Should().Be("TestQuery");
        sut.Populate.Should().BeSameAs(populate);
        sut.Filter.Should().BeSameAs(filter);
    }

    [Fact]
    public void Filter_ReturnsTrue_WhenPatternMatches()
    {
        var filter = new Func<object, string, bool?>((o, f) =>
            ((string)o).Contains(f, StringComparison.InvariantCultureIgnoreCase));

        var sut = new KnownQuery
        {
            Filter = filter,
        };

        sut.Filter("HelloWorld", "hello").Should().BeTrue();
        sut.Filter("HelloWorld", "xyz").Should().BeFalse();
    }

    [Fact]
    public void Filter_ReturnsNull_WhenNoMatch()
    {
        var filter = new Func<object, string, bool?>((o, f) =>
            o == null ? null : ((string)o).Contains(f, StringComparison.InvariantCultureIgnoreCase));

        var sut = new KnownQuery { Filter = filter };

        sut.Filter(null!, "test").Should().BeNull();
    }
}
