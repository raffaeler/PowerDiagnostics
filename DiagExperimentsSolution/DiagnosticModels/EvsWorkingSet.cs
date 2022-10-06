using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiagnosticModels
{
    public class EvsWorkingSet : EvsBaseDouble
    {
        public EvsWorkingSet(double value) : base(value) { }
        public override string Cat => "Working set";
        public override string Uom => "MB";
        public override NumberFormatInfo Format => _nfi;
    }
}
