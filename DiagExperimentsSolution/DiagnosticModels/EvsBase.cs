using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiagnosticModels
{
    public abstract class EvsBase
    {
        /// <summary>
        /// The category of this event
        /// </summary>
        public abstract string Cat { get; }

        /// <summary>
        /// The string representation of the value
        /// </summary>
        public abstract string Val { get; }

        /// <summary>
        /// Unit of measure or string.Empty
        /// </summary>
        public virtual string Uom { get; } = string.Empty;
    }
}
