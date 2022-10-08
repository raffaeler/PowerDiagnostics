using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiagnosticInvestigations
{
    public enum InvestigationKind
    {
        Unknown,

        /// <summary>
        /// Fast, but currently cannot be saved on disk
        /// </summary>
        Snapshot,

        /// <summary>
        /// Slower than Snapshot, but it can be persisted
        /// </summary>
        Dump,

        /// <summary>
        /// Currently not implemented. Does not provide all we may want to investigate
        /// </summary>
        LiveProcess
    }
}
