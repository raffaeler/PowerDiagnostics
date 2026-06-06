using ClrDiagnostics;

using DiagnosticInvestigations.Configurations;

namespace DiagnosticInvestigations.Tests;

public class InvestigationScopeTests
{
    [Fact]
    public void Constructor_SetsSessionId()
    {
        var id = Guid.NewGuid().ToString("N");
        var analyzer = Substitute.For<DiagnosticAnalyzer>();
        var scope = new InvestigationScope(id, InvestigationKind.Snapshot, analyzer);

        scope.SessionId.Should().Be(id);
    }

    [Fact]
    public void Constructor_SetsInvestigationKind()
    {
        var analyzer = Substitute.For<DiagnosticAnalyzer>();
        var scope = new InvestigationScope(Guid.NewGuid().ToString("N"), InvestigationKind.Dump, analyzer);

        scope.InvestigationKind.Should().Be(InvestigationKind.Dump);
    }

    [Fact]
    public void Constructor_SetsCreatedToNow()
    {
        var analyzer = Substitute.For<DiagnosticAnalyzer>();
        var before = DateTime.Now.AddSeconds(-1);
        var scope = new InvestigationScope(Guid.NewGuid().ToString("N"), InvestigationKind.Snapshot, analyzer);

        scope.Created.Should().BeAfter(before).And.BeOnOrBefore(DateTime.Now);
    }

    [Fact]
    public void Constructor_SetsDiagnosticAnalyzer()
    {
        var analyzer = Substitute.For<DiagnosticAnalyzer>();
        var scope = new InvestigationScope(Guid.NewGuid().ToString("N"), InvestigationKind.Snapshot, analyzer);

        scope.DiagnosticAnalyzer.Should().BeSameAs(analyzer);
    }

    [Fact]
    public void Constructor_TemporaryFile_DefaultsToNull()
    {
        var analyzer = Substitute.For<DiagnosticAnalyzer>();
        var scope = new InvestigationScope(Guid.NewGuid().ToString("N"), InvestigationKind.Snapshot, analyzer);

        scope.TemporaryFile.Should().BeNull();
    }

    [Fact]
    public void Constructor_TemporaryFile_CanBeSet()
    {
        var analyzer = Substitute.For<DiagnosticAnalyzer>();
        var tempFile = new FileInfo(@"C:\temp\dump.dmp");
        var scope = new InvestigationScope(Guid.NewGuid().ToString("N"), InvestigationKind.Dump, analyzer, tempFile);

        scope.TemporaryFile.Should().BeSameAs(tempFile);
    }
}

public class GeneralConfigurationTests
{
    [Fact]
    public void Default_DebuggingSessionsExpirationMinutes_IsTen()
    {
        var config = new GeneralConfiguration();
        config.DebuggingSessionsExpirationMinutes.Should().Be(10);
    }

    [Fact]
    public void CanSet_DebuggingSessionsExpirationMinutes()
    {
        var config = new GeneralConfiguration { DebuggingSessionsExpirationMinutes = 5 };
        config.DebuggingSessionsExpirationMinutes.Should().Be(5);
    }
}
