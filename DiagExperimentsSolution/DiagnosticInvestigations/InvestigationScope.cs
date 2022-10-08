using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ClrDiagnostics;

namespace DiagnosticInvestigations;

/// <summary>
/// This class holds the information about a specific analysis
/// It may be a dump, a snapshot or a live process analysis
/// </summary>
public record class InvestigationScope
{
    public InvestigationScope(Guid sessionId,
        InvestigationKind investigationKind,
        DiagnosticAnalyzer diagnosticAnalyzer,
        FileInfo? temporaryFile = null)
    {
        this.SessionId = sessionId;
        this.InvestigationKind = investigationKind;
        this.Created = DateTime.Now;
        this.DiagnosticAnalyzer = diagnosticAnalyzer;
    }

    public Guid SessionId { get; }
    public DateTime Created { get; }

    public InvestigationKind InvestigationKind { get; }
    public DiagnosticAnalyzer DiagnosticAnalyzer { get; }
    public FileInfo? TemporaryFile { get; }
}
