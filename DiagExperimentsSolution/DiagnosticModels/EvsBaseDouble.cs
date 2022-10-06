using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiagnosticModels
{
    public abstract class EvsBaseDouble : EvsBase
    {
        protected static NumberFormatInfo _nfi = new NumberFormatInfo { NumberGroupSeparator = "'", NumberDecimalDigits = 0 };
        private readonly double _value;

        public EvsBaseDouble(double value)
        {
            _value = value;
        }

        public virtual NumberFormatInfo Format => NumberFormatInfo.CurrentInfo;
        public override string Val => _value.ToString("n", Format);
    }
}
