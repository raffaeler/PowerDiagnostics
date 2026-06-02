using System.Diagnostics.Tracing;

using ClrDiagnostics.Triggers;

using Microsoft.Diagnostics.Tracing;

namespace ClrDiagnostics.Tests.Triggers;

public class TriggerConstructorTests
{
    [Fact]
    public void TriggerBase_Constructor_SetsIsStartedToFalse()
    {
        var trigger = new TriggerOnMemoryUsage(1234);
        trigger.IsStarted.Should().BeFalse();
    }

    [Fact]
    public void TriggerBase_Start_WithNoProviders_ReturnsFalse()
    {
        var trigger = new TriggerOnEventCounter(1234, "TestSource");
        // Clear providers by not adding any — but TriggerOnEventCounter does add one
        // Test on a raw trigger subclass
        trigger.Providers.Clear();

        var result = trigger.Start(_ => { });

        result.Should().BeFalse();
    }

    [Fact]
    public void TriggerBase_Start_WithNullTrigger_ThrowsArgumentNullException()
    {
        var trigger = new TriggerOnMemoryUsage(1234);
        trigger.Invoking(t => t.Start(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TriggerBase_Start_WithProviders_ReturnsTrue()
    {
        var trigger = new TriggerOnMemoryUsage(1234);

        var result = trigger.Start(_ => { });

        result.Should().BeTrue();
        trigger.IsStarted.Should().BeTrue();
    }

    [Fact]
    public void TriggerBase_Start_AlreadyStarted_ReturnsFalse()
    {
        var trigger = new TriggerOnMemoryUsage(1234);
        trigger.Start(_ => { });

        var result = trigger.Start(_ => { });

        result.Should().BeFalse();
    }

    [Fact]
    public void TriggerBase_Stop_WhenNotStarted_ReturnsFalse()
    {
        var trigger = new TriggerOnMemoryUsage(1234);

        var result = trigger.Stop();

        result.Should().BeFalse();
    }

    [Fact]
    public void TriggerBase_Stop_WhenStarted_ReturnsTrue()
    {
        var trigger = new TriggerOnMemoryUsage(1234);
        trigger.Start(_ => { });

        var result = trigger.Stop();

        result.Should().BeTrue();
        trigger.IsStarted.Should().BeFalse();
    }

    [Fact]
    public void TriggerOnMemoryUsage_HasProviders()
    {
        var trigger = new TriggerOnMemoryUsage(1234);
        trigger.Providers.Should().NotBeEmpty();
        trigger.Providers.Count.Should().Be(2);
    }

    [Fact]
    public void TriggerOnCpuLoad_HasProviders()
    {
        var trigger = new TriggerOnCpuLoad(1234);
        trigger.Providers.Should().NotBeEmpty();
    }

    [Fact]
    public void TriggerOnExceptions_HasProviders()
    {
        var trigger = new TriggerOnExceptions(1234);
        trigger.Providers.Should().NotBeEmpty();
    }

    [Fact]
    public void TriggerOnHttpRequests_HasProviders()
    {
        var trigger = new TriggerOnHttpRequests(1234, 1000);
        trigger.Providers.Should().NotBeEmpty();
    }

    [Fact]
    public void TriggerOnEventCounter_HasProviders()
    {
        var trigger = new TriggerOnEventCounter(1234, "CustomSource");
        trigger.Providers.Should().NotBeEmpty();
        trigger.Providers.Count.Should().Be(1);
    }

    [Fact]
    public void TriggerAll_CanBeConstructed()
    {
        // TriggerAll may have its own Providers member — test construction only
        var trigger = new TriggerAll(1234, "TestSource", "TestCounter");
        trigger.IsStarted.Should().BeFalse();
    }

    [Fact]
    public void AddKnownProvider_Throws_ForUnknownProvider()
    {
        var trigger = new TriggerOnMemoryUsage(1234);
        trigger.Invoking(t => t.AddKnownProvider((KnownProviderName)999))
            .Should().Throw<Exception>();
    }

    [Fact]
    public void AddKnownProvider_AddsProvider_ForKnownProvider()
    {
        var trigger = new TriggerOnMemoryUsage(1234);
        var initialCount = trigger.Providers.Count;

        trigger.AddKnownProvider(KnownProviderName.System_Runtime);

        trigger.Providers.Count.Should().Be(initialCount + 1);
    }
}
