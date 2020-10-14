using System;
using System.Collections.Generic;
using System.Text;

namespace DiagnosticWPF.Helpers
{
    public static class StringExtensions
    {
        public static bool FilterBy(this string text, string filter)
        {
            return text.Contains(filter, StringComparison.InvariantCultureIgnoreCase);
        }
    }
}
