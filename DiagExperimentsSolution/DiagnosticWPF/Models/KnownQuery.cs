using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using ClrDiagnostics;

namespace DiagnosticWPF.Models
{
    public class KnownQuery
    {
        public KnownQuery(Type type, string name, Func<DiagnosticAnalyzer, IEnumerable> populate,
            Func<object, string, bool?> filter)
        {
            this.Type = type;
            this.Name = name;
            this.Populate = populate;
            this.Filter = filter;
        }

        public Type Type { get; set; }
        public string Name { get; set; }
        public Func<DiagnosticAnalyzer, IEnumerable> Populate { get; set; }
        public Func<object, string, bool?> Filter { get; set; }
    }
}
