using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiagnosticModels
{
    public abstract class EvsBaseString : EvsBase
    {
        private readonly string _value;

        public EvsBaseString(string value)
        {
            _value = value;
        }

        public override string Val => _value;
    }
}
