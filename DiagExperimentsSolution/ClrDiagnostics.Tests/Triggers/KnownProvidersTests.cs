using ClrDiagnostics.Triggers;

namespace ClrDiagnostics.Tests.Triggers;

public class KnownProvidersTests
{
    [Theory]
    [InlineData(KnownProviderName.Microsoft_Windows_DotNETRuntime, "Microsoft-Windows-DotNETRuntime")]
    [InlineData(KnownProviderName.System_Runtime, "System.Runtime")]
    [InlineData(KnownProviderName.Microsoft_DotNETCore_SampleProfiler, "Microsoft-DotNETCore-SampleProfiler")]
    [InlineData(KnownProviderName.Microsoft_AspNetCore_Hosting, "Microsoft.AspNetCore.Hosting")]
    public void TryGetName_ReturnsCorrectName_ForKnownProvider(KnownProviderName provider, string expectedName)
    {
        var result = KnownProviders.TryGetName(provider, out var name);
        result.Should().BeTrue();
        name.Should().Be(expectedName);
    }

    [Fact]
    public void TryGetName_ReturnsFalse_ForInvalidProvider()
    {
        var result = KnownProviders.TryGetName((KnownProviderName)999, out var name);
        result.Should().BeFalse();
        name.Should().BeNull();
    }

    [Fact]
    public void TryGetName_AllDefinedProviders_ReturnTrue()
    {
        foreach (KnownProviderName provider in Enum.GetValues(typeof(KnownProviderName)))
        {
            KnownProviders.TryGetName(provider, out _).Should().BeTrue(
                $"Provider {provider} should have a mapped name");
        }
    }
}
