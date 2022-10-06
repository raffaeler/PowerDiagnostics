using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrDiagnostics
{
    public class DiagnosticState
    {
        private DiagnosticPhase _phase;
        public DiagnosticPhase Phase
        {
            get => _phase;
            //set { lock (this) _phase = value; }
        }

        //public bool TrySetPhase(DiagnosticPhase phase)
        //{

        //}


    }
}
