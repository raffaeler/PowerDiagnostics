using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using ClrDiagnostics;

namespace DiagnosticWPF.Models
{
    public class KnownQuery
    {
        public KnownQuery(Type type, string name, Func<DiagnosticAnalyzer, IEnumerable> populate)
        {
            this.Type = type;
            this.Name = name;
            this.Populate = populate;
        }

        public Type Type { get; set; }
        public string Name { get; set; }
        public Func<DiagnosticAnalyzer, IEnumerable> Populate { get; set; }
    }
}
