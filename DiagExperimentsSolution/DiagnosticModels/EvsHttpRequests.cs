using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiagnosticModels
{
    public class EvsHttpRequests:EvsBaseDouble
    {
        public EvsHttpRequests(double value) : base(value) { }
        public override string Cat => "HTTP Req/s";
        public override string Uom => "/sec";
        public override NumberFormatInfo Format => _nfi;
    }
}
