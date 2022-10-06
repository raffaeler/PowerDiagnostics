using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiagnosticModels
{
    public class EvsGcAllocation : EvsBaseDouble
    {
        public EvsGcAllocation(double value) : base(value) { }
        public override string Cat => "Last GC Allocation";
        public override string Uom => "bytes";
        public override NumberFormatInfo Format => _nfi;
    }
}
