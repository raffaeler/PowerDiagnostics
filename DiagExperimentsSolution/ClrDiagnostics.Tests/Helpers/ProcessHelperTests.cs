using System.Diagnostics;

using ClrDiagnostics.Helpers;

namespace ClrDiagnostics.Tests.Helpers;

/// <summary>
/// Unit tests for ProcessHelper logic. Tests that require real Process instances
/// (which are sealed and cannot be mocked) are covered by integration tests.
/// </summary>
public class ProcessHelperTests
{
    private static IProcessProvider CreateProvider(Process[]? processes = null)
    {
        var provider = Substitute.For<IProcessProvider>();
        provider.GetProcessesByName(Arg.Any<string>()).Returns(processes ?? Array.Empty<Process>());
        return provider;
    }

    [Fact]
    public void GetProcess_ReturnsNull_WhenZeroFound()
    {
        var provider = CreateProvider(Array.Empty<Process>());
        var sut = new ProcessHelper(provider);
        sut.GetProcess("test").Should().BeNull();
    }

    [Fact]
    public void GetProcess_ReturnsNull_WhenMultipleFound()
    {
        // Use null array entries to test the length check without real Process objects
        var provider = CreateProvider(new Process[2]);
        var sut = new ProcessHelper(provider);
        sut.GetProcess("test").Should().BeNull();
    }

    [Fact]
    public void GetOrStartProcess_StartsNewProcess_WhenZeroFound()
    {
        var provider = CreateProvider(Array.Empty<Process>());
        var sut = new ProcessHelper(provider);
        provider.Start(Arg.Any<ProcessStartInfo>()).Returns((Process)null!);

        sut.GetOrStartProcess("test", "test.exe");

        provider.Received(1).Start(Arg.Is<ProcessStartInfo>(s => s.FileName == "test.exe"));
    }

    [Fact]
    public void GetOrStartProcess_ReturnsNull_WhenMultipleFound()
    {
        var provider = CreateProvider(new Process[2]);
        var sut = new ProcessHelper(provider);
        sut.GetOrStartProcess("test", "test.exe").Should().BeNull();
    }

    [Fact]
    public void GetDotnetProcesses_ReturnsEmpty_WhenNoProcesses()
    {
        var provider = Substitute.For<IProcessProvider>();
        provider.GetPublishedProcesses().Returns(Array.Empty<int>());
        var sut = new ProcessHelper(provider);
        sut.GetDotnetProcesses().Should().BeEmpty();
    }

    [Fact]
    public void GetDotnetProcesses_SkipsNullResults()
    {
        var provider = Substitute.For<IProcessProvider>();
        provider.GetPublishedProcesses().Returns(new[] { 100 });
        provider.GetProcessById(100).Returns((Process)null!);
        var sut = new ProcessHelper(provider);
        sut.GetDotnetProcesses().Should().BeEmpty();
    }
}
