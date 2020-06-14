using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.Diagnostics.Runtime;
using ClrDiagnostics.Extensions;
using ClrDiagnostics.Models;

namespace ClrDiagnostics
{
    public partial class DiagnosticAnalyzer
    {
        public IEnumerable<(ClrStaticField field, ClrObject obj)> GetStaticFields()
        {
            return _clrRuntime
                .GetConstructedTypeDefinitions(t => t.StaticFields.Length > 0)
                .SelectMany(t => t.StaticFields)
                .Where(f => f.IsObjectReference)
                .Select(f => (field: f, value: f.ReadObject(MainAppDomain)))
                .Where(t => !t.value.IsNull);
        }

        public IEnumerable<(ClrStaticField field, ClrObject obj, UInt64 size)> GetStaticFieldsWithGraphSize()
        {
            return _clrRuntime
                .GetConstructedTypeDefinitions(t => t.StaticFields.Length > 0)
                .SelectMany(t => t.StaticFields)
                .Where(f => f.IsObjectReference)
                .Select(f => (field: f, value: f.ReadObject(MainAppDomain)))
                .Select(t => (field: t.field, value: t.value, size: t.value.GetGraphSize()))
                .Where(t => !t.value.IsNull)
                .OrderByDescending(t => t.size);
        }

        public IEnumerable<(ClrStaticField field, ClrObject obj, UInt64 size, IEnumerable<ClrGraphNode> graph)> GetStaticFieldsWithGraphAndSize()
        {
            return _clrRuntime
                .GetConstructedTypeDefinitions(t => t.StaticFields.Length > 0)
                .SelectMany(t => t.StaticFields)
                .Where(f => f.IsObjectReference)
                .Select(f => (field: f, value: f.ReadObject(MainAppDomain)))
                .Select(t => (field: t.field, value: t.value, size: t.value.GetGraphSize(),
                                graph: ClrGraph.CreateForChildren(t.value)))
                .Where(t => !t.value.IsNull)
                .OrderByDescending(t => t.size);
        }
    }
}
