// TraceEventExtensions.GetPayload requires real TraceEvent objects from a live
// EventPipe session or trace file — these are integration-level tests.
// Moved to Phase 5 (Integration tests) for coverage against real EventPipe data.
/*
using System.Collections;

using ClrDiagnostics.Extensions;

using Microsoft.Diagnostics.Tracing;

namespace ClrDiagnostics.Tests.Extensions;

public class TraceEventExtensionsTests
{
    [Fact]
    public void GetPayload_ReturnsInnerPayload_WhenPresent()
    {
        var innerPayload = new Dictionary<string, object>
        {
            { "Name", "working-set" },
            { "Mean", 1024.0 },
        };

        var outerPayload = new Dictionary<string, object>
        {
            { "Payload", innerPayload },
        };

        var traceEvent = Substitute.For<TraceEvent>();
        traceEvent.PayloadNames.Returns(new[] { "Key" });
        traceEvent.PayloadValue(0).Returns(outerPayload);

        var result = traceEvent.GetPayload();

        result.Should().NotBeNull();
        result.Should().BeSameAs(innerPayload);
        result!["Name"].Should().Be("working-set");
    }

    [Fact]
    public void GetPayload_ReturnsNull_WhenNoPayloadNames()
    {
        var traceEvent = Substitute.For<TraceEvent>();
        traceEvent.PayloadNames.Returns(Array.Empty<string>());

        var result = traceEvent.GetPayload();

        result.Should().BeNull();
    }

    [Fact]
    public void GetPayload_ReturnsNull_WhenPayloadValueNotDictionary()
    {
        var traceEvent = Substitute.For<TraceEvent>();
        traceEvent.PayloadNames.Returns(new[] { "Key" });
        traceEvent.PayloadValue(0).Returns("not a dictionary");

        var result = traceEvent.GetPayload();

        result.Should().BeNull();
    }

    [Fact]
    public void GetPayload_ReturnsNull_WhenNoPayloadKey()
    {
        var outerWithoutPayload = new Dictionary<string, object>
        {
            { "OtherKey", "value" },
        };

        var traceEvent = Substitute.For<TraceEvent>();
        traceEvent.PayloadNames.Returns(new[] { "Key" });
        traceEvent.PayloadValue(0).Returns(outerWithoutPayload);

        var result = traceEvent.GetPayload();

        result.Should().BeNull();
    }
}
*/
