using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiagnosticModels
{
    public class EvsCpu : EvsBaseDouble
    {
        public EvsCpu(double value) : base(value) { }

        public override string Cat => "CPU";
        public override string Uom => "%";
    }
}
