using System.Reflection;

using Microsoft.Diagnostics.Runtime;

namespace ClrDiagnostics.Tests.Analysis;

public class GCRootPathDeduplicationTests
{
    private static GCRoot.ChainLink CreateChainLink(ulong obj, GCRoot.ChainLink? next = null)
    {
        // ClrMD v4: ChainLink has a public parameterless constructor with writable properties
        return new GCRoot.ChainLink { Object = obj, Next = next };
    }

    private static ClrObject CreateClrObject(ulong address)
    {
        var type = typeof(ClrObject);
        var ctor = type.GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            new[] { typeof(ulong), typeof(ClrType) },
            null)!;
        return (ClrObject)ctor.Invoke(new object?[] { address, null });
    }

    private static ClrRoot CreateRoot(ulong address, ClrRootKind kind)
    {
        var obj = CreateClrObject(address);
        return new ClrRoot(address, obj, kind, false, false);
    }

    [Fact]
    public void DeduplicatePaths_ReturnsEmpty_WhenInputIsEmpty()
    {
        var input = new List<(ClrRoot Root, GCRoot.ChainLink Path)>();
        var result = DiagnosticAnalyzer.DeduplicatePaths(input);
        result.Should().BeEmpty();
    }

    [Fact]
    public void DeduplicatePaths_ReturnsSingleItem_WhenInputHasOneItem()
    {
        var root = CreateRoot(0x1000, ClrRootKind.Stack);
        var path = CreateChainLink(0x2000);
        var input = new List<(ClrRoot Root, GCRoot.ChainLink Path)> { (root, path) };

        var result = DiagnosticAnalyzer.DeduplicatePaths(input);

        result.Should().HaveCount(1);
    }

    [Fact]
    public void DeduplicatePaths_RemovesExactDuplicates()
    {
        var root = CreateRoot(0x1000, ClrRootKind.Stack);
        var path = CreateChainLink(0x2000, CreateChainLink(0x3000));
        var input = new List<(ClrRoot Root, GCRoot.ChainLink Path)>
        {
            (root, path),
            (root, path),
        };

        var result = DiagnosticAnalyzer.DeduplicatePaths(input);

        result.Should().HaveCount(1);
    }

    [Fact]
    public void DeduplicatePaths_RemovesDifferentRootAddresses_WithSamePath()
    {
        var root1 = CreateRoot(0x1000, ClrRootKind.Stack);
        var root2 = CreateRoot(0x1001, ClrRootKind.Stack);
        var path = CreateChainLink(0x2000, CreateChainLink(0x3000));
        var input = new List<(ClrRoot Root, GCRoot.ChainLink Path)>
        {
            (root1, path),
            (root2, path),
        };

        var result = DiagnosticAnalyzer.DeduplicatePaths(input);

        result.Should().HaveCount(1);
    }

    [Fact]
    public void DeduplicatePaths_KeepsDifferentRootKinds_WithSamePath()
    {
        var root1 = CreateRoot(0x1000, ClrRootKind.Stack);
        var root2 = CreateRoot(0x1000, ClrRootKind.StrongHandle);
        var path = CreateChainLink(0x2000, CreateChainLink(0x3000));
        var input = new List<(ClrRoot Root, GCRoot.ChainLink Path)>
        {
            (root1, path),
            (root2, path),
        };

        var result = DiagnosticAnalyzer.DeduplicatePaths(input);

        result.Should().HaveCount(2);
    }

    [Fact]
    public void DeduplicatePaths_KeepsDifferentPaths_WithSameRoot()
    {
        var root = CreateRoot(0x1000, ClrRootKind.Stack);
        var path1 = CreateChainLink(0x2000, CreateChainLink(0x3000));
        var path2 = CreateChainLink(0x2000, CreateChainLink(0x4000));
        var input = new List<(ClrRoot Root, GCRoot.ChainLink Path)>
        {
            (root, path1),
            (root, path2),
        };

        var result = DiagnosticAnalyzer.DeduplicatePaths(input);

        result.Should().HaveCount(2);
    }

    [Fact]
    public void GetPathKey_ReturnsDifferentKeys_ForDifferentPaths()
    {
        var root1 = CreateRoot(0x1000, ClrRootKind.Stack);
        var root2 = CreateRoot(0x1001, ClrRootKind.StrongHandle);
        var path1 = CreateChainLink(0x2000, CreateChainLink(0x3000));
        var path2 = CreateChainLink(0x2000, CreateChainLink(0x4000));

        var key1 = DiagnosticAnalyzer.GetPathKey(root1, path1);
        var key2 = DiagnosticAnalyzer.GetPathKey(root2, path2);

        key1.Should().NotBe(key2);
    }

    [Fact]
    public void GetPathKey_ReturnsSameKey_ForIdenticalPaths()
    {
        var root = CreateRoot(0x1000, ClrRootKind.Stack);
        var path = CreateChainLink(0x2000, CreateChainLink(0x3000));

        var key1 = DiagnosticAnalyzer.GetPathKey(root, path);
        var key2 = DiagnosticAnalyzer.GetPathKey(root, path);

        key1.Should().Be(key2);
    }

    [Fact]
    public void FilterAndDeduplicatePaths_ReturnsAllItems_WhenFlagIsFalse()
    {
        var root1 = CreateRoot(0x1000, ClrRootKind.Stack);
        var root2 = CreateRoot(0x1000, ClrRootKind.Stack); // duplicate
        var path = CreateChainLink(0x2000);
        var input = new List<(ClrRoot Root, GCRoot.ChainLink Path)> { (root1, path), (root2, path) };

        var result = DiagnosticAnalyzer.FilterAndDeduplicatePaths(input, deduplicateRegisterRoots: false);

        result.Should().HaveCount(2);
    }

    [Fact]
    public void FilterAndDeduplicatePaths_FiltersRegisterRoots_WhenFlagIsTrue()
    {
        var registerRoot = CreateRoot(0x0, ClrRootKind.Stack);
        var stackRoot = CreateRoot(0x1000, ClrRootKind.Stack);
        var path = CreateChainLink(0x2000);
        var input = new List<(ClrRoot Root, GCRoot.ChainLink Path)> { (registerRoot, path), (stackRoot, path) };

        var result = DiagnosticAnalyzer.FilterAndDeduplicatePaths(input, deduplicateRegisterRoots: true);

        result.Should().HaveCount(1);
        result.Single().Root.Address.Should().Be(0x1000);
    }

    [Fact]
    public void FilterAndDeduplicatePaths_Deduplicates_AfterFiltering()
    {
        var registerRoot = CreateRoot(0x0, ClrRootKind.Stack);
        var stackRoot1 = CreateRoot(0x1000, ClrRootKind.Stack);
        var stackRoot2 = CreateRoot(0x1001, ClrRootKind.Stack);
        var path = CreateChainLink(0x2000, CreateChainLink(0x3000));
        var input = new List<(ClrRoot Root, GCRoot.ChainLink Path)>
        {
            (registerRoot, path),
            (stackRoot1, path),
            (stackRoot2, path),
        };

        var result = DiagnosticAnalyzer.FilterAndDeduplicatePaths(input, deduplicateRegisterRoots: true);

        result.Should().HaveCount(1);
        result.Single().Root.Address.Should().Be(0x1000);
    }

    [Fact]
    public void FilterAndDeduplicatePaths_ReturnsEmpty_WhenOnlyRegisterRoots()
    {
        var registerRoot = CreateRoot(0x0, ClrRootKind.Stack);
        var path = CreateChainLink(0x2000);
        var input = new List<(ClrRoot Root, GCRoot.ChainLink Path)> { (registerRoot, path) };

        var result = DiagnosticAnalyzer.FilterAndDeduplicatePaths(input, deduplicateRegisterRoots: true);

        result.Should().BeEmpty();
    }
}
