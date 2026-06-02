using System.Globalization;

namespace DiagnosticModels.Tests;

public class EvsBaseDoubleTests
{
    /// <summary>
    /// A concrete subclass for testing EvsBaseDouble formatting with a known NumberFormatInfo.
    /// </summary>
    private class TestEvs : EvsBaseDouble
    {
        private readonly NumberFormatInfo _format;
        public TestEvs(double value, NumberFormatInfo format) : base(value) { _format = format; }
        public override string Cat => "Test";
        public override NumberFormatInfo Format => _format;
    }

    [Fact]
    public void Constructor_StoresValue()
    {
        var sut = new TestEvs(42.0, NumberFormatInfo.InvariantInfo);
        sut.Val.Should().Be("42.00");
    }

    [Fact]
    public void Val_UsesFormatProperty()
    {
        var nfi = new NumberFormatInfo { NumberGroupSizes = new[] { 3 }, NumberGroupSeparator = "'", NumberDecimalDigits = 0 };
        var sut = new TestEvs(1235, nfi);
        sut.Val.Should().Be("1'235");
    }

    [Fact]
    public void Val_WithZero()
    {
        var nfi = new NumberFormatInfo { NumberGroupSizes = new[] { 3 }, NumberGroupSeparator = "'", NumberDecimalDigits = 0 };
        var sut = new TestEvs(0, nfi);
        sut.Val.Should().Be("0");
    }

    [Fact]
    public void Val_WithLargeNumber_UsesGroupSeparator()
    {
        var nfi = new NumberFormatInfo { NumberGroupSizes = new[] { 3 }, NumberGroupSeparator = "'", NumberDecimalDigits = 0 };
        var sut = new TestEvs(1000000, nfi);
        sut.Val.Should().Be("1'000'000");
    }

    [Fact]
    public void Val_UsesInvariantFormat_WhenNoOverride()
    {
        var sut = new EvsCpu(1234.5);
        sut.Val.Should().Be(1234.5.ToString("n", NumberFormatInfo.CurrentInfo));
    }

    [Fact]
    public void Val_UsesNfiFormat_WhenOverridden()
    {
        var sut = new EvsWorkingSet(1234);
        sut.Val.Should().Be("1'234");
    }

    [Fact]
    public void EvsCpu_HasCorrectCategoryAndUnit()
    {
        var sut = new EvsCpu(50.0);
        sut.Cat.Should().Be("CPU");
        sut.Uom.Should().Be("%");
    }

    [Fact]
    public void EvsWorkingSet_HasCorrectCategoryAndUnit()
    {
        var sut = new EvsWorkingSet(256.0);
        sut.Cat.Should().Be("Working set");
        sut.Uom.Should().Be("MB");
    }

    [Fact]
    public void EvsGcAllocation_HasCorrectCategoryAndUnit()
    {
        var sut = new EvsGcAllocation(1024);
        sut.Cat.Should().Be("Last GC Allocation");
        sut.Uom.Should().Be("bytes");
    }

    [Fact]
    public void EvsHttpRequests_HasCorrectCategoryAndUnit()
    {
        var sut = new EvsHttpRequests(100);
        sut.Cat.Should().Be("HTTP Req/s");
        sut.Uom.Should().Be("/sec");
    }

    [Fact]
    public void EvsCustomHeader_HasCorrectCategory()
    {
        var sut = new EvsCustomHeader(42);
        sut.Cat.Should().Be("Custom header");
        sut.Uom.Should().BeEmpty();
    }

    [Fact]
    public void EvsNfiSubclasses_FormatWithThousandsSeparator()
    {
        var sut = new EvsGcAllocation(9999);
        sut.Val.Should().Be("9'999");
    }
}
