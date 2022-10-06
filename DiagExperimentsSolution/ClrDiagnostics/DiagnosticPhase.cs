using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrDiagnostics
{
    public enum DiagnosticPhase
    {
        Ready,
        Watching,
        AnalyzingSnap,
        AnalyzingDump,
    }
}
