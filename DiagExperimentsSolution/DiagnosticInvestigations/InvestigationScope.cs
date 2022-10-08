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
    public InvestigationScope(InvestigationKind investigationKind,
        DiagnosticAnalyzer diagnosticAnalyzer,
        FileInfo? temporaryFile = null)
    {
        this.InvestigationKind = investigationKind;
        this.When = DateTime.Now;
        this.DiagnosticAnalyzer = diagnosticAnalyzer;
    }

    public DateTime When { get; }
    public InvestigationKind InvestigationKind { get; }
    public DiagnosticAnalyzer DiagnosticAnalyzer { get; }
    public FileInfo? TemporaryFile { get; }
}
