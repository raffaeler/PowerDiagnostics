using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiagnosticModels
{
    public class EvsException : EvsBaseString
    {
        public EvsException(string value) : base(value) { }
        public override string Cat => "Last first-chance Exception";

    }
}
