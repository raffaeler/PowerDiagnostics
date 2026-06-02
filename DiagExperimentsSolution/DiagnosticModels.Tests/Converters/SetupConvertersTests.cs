using System.Text.Json;

using DiagnosticModels.Converters;

namespace DiagnosticModels.Tests.Converters;

public class SetupConvertersTests
{
    [Fact]
    public void CreateOptions_HasCamelCaseNamingPolicy()
    {
        var options = SetupConverters.CreateOptions();
        options.PropertyNamingPolicy.Should().Be(JsonNamingPolicy.CamelCase);
    }

    [Fact]
    public void CreateOptions_HasAllRequiredConverters()
    {
        var options = SetupConverters.CreateOptions();

        options.Converters.Should().Contain(c => c is ClrObjectConverter);
        options.Converters.Should().Contain(c => c is ClrTypeConverter);
        options.Converters.Should().Contain(c => c is ClrModuleConverter);
        options.Converters.Should().Contain(c => c is ClrRootConverter);
        options.Converters.Should().Contain(c => c is ClrStackFrameConverter);
        options.Converters.Should().Contain(c => c is ClrInstanceFieldConverter);
        options.Converters.Should().Contain(c => c is ClrStaticFieldConverter);
        options.Converters.Should().Contain(c => c is ClrThreadConverter);
        options.Converters.Should().Contain(c => c is ClrExceptionConverter);
    }

    [Fact]
    public void CreateOptions_HasNineConverters()
    {
        var options = SetupConverters.CreateOptions();
        options.Converters.Count.Should().Be(9);
    }

    [Fact]
    public void ConfigureOptions_SetsCamelCaseNamingPolicy()
    {
        var options = new JsonSerializerOptions();
        SetupConverters.ConfigureOptions(options);
        options.PropertyNamingPolicy.Should().Be(JsonNamingPolicy.CamelCase);
    }

    [Fact]
    public void ConfigureOptions_ReplacesExistingConverters()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new ClrObjectConverter());
        SetupConverters.ConfigureOptions(options);

        options.Converters.Count.Should().Be(9);
        options.Converters[0].Should().BeOfType<ClrExceptionConverter>();
    }
}
