using ClrDiagnostics;

using DiagnosticInvestigations.Configurations;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiagnosticInvestigations.Tests;

public class InvestigationStateTests
{
    private static InvestigationState CreateSut(
        int expirationMinutes = 10,
        ILogger<InvestigationState>? logger = null)
    {
        var config = Substitute.For<IOptions<GeneralConfiguration>>();
        config.Value.Returns(new GeneralConfiguration { DebuggingSessionsExpirationMinutes = expirationMinutes });

        return new InvestigationState(
            logger ?? Substitute.For<ILogger<InvestigationState>>(),
            config);
    }

    [Fact]
    public void AddSnapshot_CreatesSession_WithSnapshotKind()
    {
        var sut = CreateSut();
        var analyzer = Substitute.For<DiagnosticAnalyzer>();

        var sessionId = sut.AddSnapshot(analyzer);

        var scope = sut.GetInvestigationScope(sessionId);
        scope.Should().NotBeNull();
        scope!.InvestigationKind.Should().Be(InvestigationKind.Snapshot);
        scope.DiagnosticAnalyzer.Should().BeSameAs(analyzer);
    }

    [Fact]
    public void AddDump_CreatesSession_WithDumpKind()
    {
        var sut = CreateSut();
        var analyzer = Substitute.For<DiagnosticAnalyzer>();

        var sessionId = sut.AddDump(analyzer);

        var scope = sut.GetInvestigationScope(sessionId);
        scope.Should().NotBeNull();
        scope!.InvestigationKind.Should().Be(InvestigationKind.Dump);
    }

    [Fact]
    public void AddSnapshotAndAddDump_ReturnUniqueIds()
    {
        var sut = CreateSut();
        var analyzer = Substitute.For<DiagnosticAnalyzer>();

        var id1 = sut.AddSnapshot(analyzer);
        var id2 = sut.AddDump(analyzer);

        id1.Should().NotBe(id2);
    }

    [Fact]
    public void GetInvestigationScope_ReturnsNull_ForUnknownSession()
    {
        var sut = CreateSut();
        sut.GetInvestigationScope(Guid.NewGuid().ToString("N")).Should().BeNull();
    }

    [Fact]
    public void GetActiveSessions_ReturnsAllAddedSessions()
    {
        var sut = CreateSut();
        sut.AddSnapshot(Substitute.For<DiagnosticAnalyzer>());
        sut.AddDump(Substitute.For<DiagnosticAnalyzer>());

        sut.GetActiveSessions().Count.Should().Be(2);
    }

    [Fact]
    public void MarkClientConnection_IncrementsRefCount_AndClearsOrphaned()
    {
        var sut = CreateSut();
        sut.ClientRefCount.Should().Be(0);

        sut.MarkClientConnection();
        sut.ClientRefCount.Should().Be(1);
        sut.Orphaned.Should().BeNull();
    }

    [Fact]
    public void MarkClientConnection_Twice_IncrementsToTwo()
    {
        var sut = CreateSut();
        sut.MarkClientConnection();
        sut.MarkClientConnection();
        sut.ClientRefCount.Should().Be(2);
    }

    [Fact]
    public void MarkClientDisconnection_SetsOrphaned_WhenRefCountReachesZero()
    {
        var sut = CreateSut();
        sut.MarkClientConnection();
        sut.MarkClientDisconnection();

        sut.ClientRefCount.Should().Be(0);
        sut.Orphaned.Should().NotBeNull();
    }

    [Fact]
    public void MarkClientDisconnection_DoesNotSetOrphaned_WhenRefCountAboveZero()
    {
        var sut = CreateSut();
        sut.MarkClientConnection();
        sut.MarkClientConnection();
        sut.MarkClientDisconnection();

        sut.ClientRefCount.Should().Be(1);
        sut.Orphaned.Should().BeNull();
    }

    [Fact]
    public void MarkClientConnection_AfterDisconnectionToZero_ClearsOrphaned()
    {
        var sut = CreateSut();
        sut.MarkClientConnection();
        sut.MarkClientDisconnection();
        sut.Orphaned.Should().NotBeNull();

        sut.MarkClientConnection();
        sut.Orphaned.Should().BeNull();
        sut.ClientRefCount.Should().Be(1);
    }

    [Fact]
    public void ClearSessionIfExpired_DoesNothing_WhenNotOrphaned()
    {
        var sut = CreateSut();
        sut.AddSnapshot(Substitute.For<DiagnosticAnalyzer>());

        sut.ClearSessionIfExpired();

        sut.GetActiveSessions().Count.Should().Be(1);
    }

    [Fact]
    public void ClearSessionIfExpired_DoesNothing_WhenOrphanedButNotExpired()
    {
        var sut = CreateSut(expirationMinutes: 10);
        sut.MarkClientConnection();
        sut.MarkClientDisconnection(); // orphaned now, but only a moment ago
        sut.AddSnapshot(Substitute.For<DiagnosticAnalyzer>());

        sut.ClearSessionIfExpired();

        sut.GetActiveSessions().Count.Should().Be(1);
    }

    [Fact]
    public void ClearSessionIfExpired_ClearsSessions_WhenExpired()
    {
        // Use 1 minute expiry, orphaned 2 minutes ago
        var sut = CreateSut(expirationMinutes: 0);
        sut.MarkClientConnection();
        sut.MarkClientDisconnection(); // orphaned ~ DateTime.Now
        var analyzer = Substitute.For<DiagnosticAnalyzer>();
        sut.AddSnapshot(analyzer);

        // Since expiration is 0 minutes, any orphan time triggers cleanup
        sut.ClearSessionIfExpired();

        sut.GetActiveSessions().Should().BeEmpty();
        analyzer.Received(1).Dispose();
    }

    [Fact]
    public void ClearSessionIfExpired_DisposesAllAnalyzers()
    {
        var sut = CreateSut(expirationMinutes: 0);
        sut.MarkClientConnection();
        sut.MarkClientDisconnection();
        var a1 = Substitute.For<DiagnosticAnalyzer>();
        var a2 = Substitute.For<DiagnosticAnalyzer>();
        sut.AddSnapshot(a1);
        sut.AddDump(a2);

        sut.ClearSessionIfExpired();

        a1.Received(1).Dispose();
        a2.Received(1).Dispose();
    }

    [Fact]
    public void ClearSessionIfExpired_DoesNotDispose_WhenNotExpired()
    {
        var sut = CreateSut(expirationMinutes: 10);
        var analyzer = Substitute.For<DiagnosticAnalyzer>();
        sut.AddSnapshot(analyzer);

        sut.ClearSessionIfExpired();

        analyzer.DidNotReceive().Dispose();
    }
}
