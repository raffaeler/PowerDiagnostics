using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiagnosticModels
{
    public class EvsCustomHeader : EvsBaseDouble
    {
        public EvsCustomHeader(double value) : base(value) { }
        public override string Cat => "Custom header";
        public override NumberFormatInfo Format => _nfi;

    }
}
