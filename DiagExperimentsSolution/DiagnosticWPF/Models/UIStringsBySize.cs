using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Diagnostics.Runtime;

namespace DiagnosticWPF.Models
{
    public class UIStringsBySize
    {
        public ClrObject Obj { get; set; }
        public string Text { get; set; }
        public long Size => Text.Length;
    }
}
