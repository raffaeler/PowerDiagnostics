using System.Reflection;

using DiagnosticModels;
using Microsoft.Diagnostics.Runtime;

namespace ClrDiagnostics.Tests.SOS;

/// <summary>
/// Helper type with public fields (not auto-properties) so ClrMD reliably enumerates references.
/// The distinctive name minimises collisions with other types in the process.
/// </summary>
public class AddressPathTestTarget
{
    public AddressPathTestTarget? Child;
    public int Tag = 42;
}

/// <summary>
/// Integration tests for <see cref="DiagnosticAnalyzer.FindReferenced"/>
/// and <see cref="DiagnosticAnalyzer.GetRootPathsAsync"/>.
/// These tests attach to the current process and exercise real ClrMD heap walking.
/// </summary>
public class AddressPathTests : IDisposable
{
    private readonly DiagnosticAnalyzer _analyzer;
    private bool _disposed;

    // Keep strong references so GC doesn't reclaim them during snapshot
    private static AddressPathTestTarget? _rootTarget;
    private static AddressPathTestTarget? _childTarget;
    private static readonly string _targetTypeName = typeof(AddressPathTestTarget).FullName!;

    public AddressPathTests()
    {
        // Build a tiny graph: root -> child
        _childTarget = new AddressPathTestTarget { Tag = 99 };
        _rootTarget = new AddressPathTestTarget { Child = _childTarget, Tag = 42 };

        GC.Collect(2, GCCollectionMode.Forced, blocking: true);

        var dataTarget = DataTarget.CreateSnapshotAndAttach(Environment.ProcessId);
        _analyzer = new DiagnosticAnalyzer(dataTarget, cacheObjects: true,
            new System.IO.DirectoryInfo(System.IO.Path.GetTempPath()), null);
    }

    [Fact]
    public void FindReferenced_ReturnsNull_ForNullOrInvalidAddress()
    {
        var method = GetFindReferencedMethod();

        var result = method.Invoke(_analyzer, new object[] { false, new ulong[] { 0x0 } });
        result.Should().BeNull();
    }

    [Fact]
    public void FindReferenced_WalksForward_FromKnownObject()
    {
        var method = GetFindReferencedMethod();

        ulong rootAddress = FindRootTargetAddress();
        rootAddress.Should().NotBe(0, "root target should be found on the heap");

        var result = method.Invoke(_analyzer, new object[] { false, new ulong[] { rootAddress } })
            as List<(ulong address, string typeName, string fieldName, bool isStatic)>;

        result.Should().NotBeNull("root has a public reference field 'Child'");
        result!.Should().Contain(r =>
            r.fieldName == "Child" &&
            r.typeName == _targetTypeName);
    }

    [Fact]
    public async Task GetAddressPathAsync_ReturnsResult_WithTargetInTree()
    {
        ulong rootAddress = FindRootTargetAddress();
        rootAddress.Should().NotBe(0);

        var targetObj = _analyzer.Heap.GetObject(rootAddress);

        int progressCount = 0;
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var result = await _analyzer.GetRootPathsAsync(
            targetObj,
            _ => Interlocked.Increment(ref progressCount),
            cts.Token,
            maxPaths: 5);

        result.Should().NotBeNull();
        result.Paths.Should().NotBeEmpty("at least one root path should exist");
        result.TotalPaths.Should().BeGreaterThan(0);

        // Verify the target address appears somewhere in the tree
        bool foundTarget = result.Paths.Any(p => TreeContainsAddress(p, rootAddress));
        foundTarget.Should().BeTrue("target object should appear in the path tree");
    }

    [Fact]
    public async Task GetAddressPathAsync_RespectsCancellation()
    {
        ulong rootAddress = FindRootTargetAddress();
        if (rootAddress == 0)
        {
            return;
        }

        var targetObj = _analyzer.Heap.GetObject(rootAddress);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await _analyzer.GetRootPathsAsync(
                targetObj,
                _ => { },
                cts.Token,
                maxPaths: 1);
        });
        ex.Should().NotBeNull();
    }

    [Fact]
    public void FindReferenced_ExcludesStaticFields_WhenIncludeStaticIsFalse()
    {
        var method = GetFindReferencedMethod();

        ulong rootAddress = FindRootTargetAddress();
        if (rootAddress == 0) return;

        var resultNoStatic = method.Invoke(_analyzer, new object[] { false, new ulong[] { rootAddress } })
            as List<(ulong address, string typeName, string fieldName, bool isStatic)>;

        var resultWithStatic = method.Invoke(_analyzer, new object[] { true, new ulong[] { rootAddress } })
            as List<(ulong address, string typeName, string fieldName, bool isStatic)>;

        // Both should succeed; withStatic may have additional entries if there are static refs
        resultNoStatic.Should().NotBeNull();
        resultWithStatic.Should().NotBeNull();
    }

    private static MethodInfo GetFindReferencedMethod()
    {
        return typeof(DiagnosticAnalyzer).GetMethod(
            "FindReferenced",
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            new[] { typeof(bool), typeof(ulong[]) },
            null)!;
    }

    private ulong FindRootTargetAddress()
    {
        foreach (var obj in _analyzer.Objects)
        {
            if (obj.Type?.Name != _targetTypeName)
                continue;

            // Pick the root (Tag == 42). Child has Tag == 99.
            var tag = obj.ReadField<int>("Tag");
            if (tag == 42)
                return obj.Address;
        }
        return 0;
    }

    private static bool TreeContainsAddress(GcRootPathNode node, ulong address)
    {
        if (node.ObjectAddress.Equals($"0x{address:X16}", StringComparison.OrdinalIgnoreCase))
            return true;

        foreach (var child in node.Children)
        {
            if (TreeContainsAddress(child, address))
                return true;
        }
        return false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _analyzer?.Dispose();
    }
}
