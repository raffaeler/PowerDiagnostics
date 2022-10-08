using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ClrDiagnostics;

namespace DiagnosticInvestigations
{
    public class InvestigationState
    {
        private ConcurrentDictionary<Guid, InvestigationScope> _analyzer = new();

        public Guid AddSnapshot(DiagnosticAnalyzer analyzer)
        {
            Guid session = Guid.NewGuid();
            InvestigationScope scope = new(InvestigationKind.Snapshot, analyzer);
            _analyzer[session] = scope;
            return session;
        }

        public Guid AddDump(DiagnosticAnalyzer analyzer)
        {
            Guid session = Guid.NewGuid();
            InvestigationScope scope = new(InvestigationKind.Dump, analyzer);
            _analyzer[session] = scope;
            return session;
        }

    }
}
