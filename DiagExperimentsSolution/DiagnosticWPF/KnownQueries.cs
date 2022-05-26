using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DiagnosticWPF.Helpers;
using DiagnosticWPF.Models;

using Microsoft.Diagnostics.Runtime;

namespace DiagnosticWPF
{
    internal static class KnownQueries
    {
        public static IList<KnownQuery> CreateQueries()
        {
            var queries = new List<KnownQuery>();

            queries.Add(new()
            {
                Type = typeof(UIDumpHeapStat),
                Name = "DumpHeapStat",
                Populate = a => a.DumpHeapStat(0)
                                .Select(t => new UIDumpHeapStat()
                                {
                                    Type = t.type,
                                    Objects = t.objects,
                                    GraphSize = t.size,
                                })
                                .ToList(),
                Filter = (o, f) => ((UIDumpHeapStat)o)?.Type?.Name?.FilterBy(f),
            });

            queries.Add(new()
            {
                Type = typeof(UIStaticFields),
                Name = "GetStaticFieldsWithGraphAndSize",
                Populate = a => a.GetStaticFieldsWithGraphAndSize()
                                .Select(t => new UIStaticFields()
                                {
                                    Field = t.field,
                                    Obj = t.obj,
                                    Size = (long)t.size,
                                })
                                .ToList(),
                Filter = (o, f) => ((UIStaticFields)o)?.Obj.Type?.Name?.FilterBy(f)
            });

            queries.Add(new()
            {
                Type = typeof(UIDupStrings),
                Name = "GetDuplicateStrings", 
                Populate = a => a.GetDuplicateStrings()
                                .Select(t => new UIDupStrings()
                                {
                                    Text = t.Key,
                                    Count = t.Value,
                                })
                                .ToList(),
                Filter = (o, f) => ((UIDupStrings)o)?.Text?.FilterBy(f)
            });

            queries.Add(new()
            {
                Type = typeof(UIStringsBySize),
                Name = "GetStringsBySize",
                Populate = a => a.GetStringsBySize(0)
                                .Select(t => new UIStringsBySize()
                                {
                                    Obj = t.obj,
                                    Text = t.text,
                                })
                                .ToList(),
                Filter = (o, f) => ((UIStringsBySize)o)?.Text?.FilterBy(f),
            });

            queries.Add(new()
            {
                Type = typeof(ClrModule),
                Name = "Modules",
                Populate = a => a.Modules.ToList(),
                Filter = (o, f) => ((ClrModule)o)?.Name?.FilterBy(f)
            });

            queries.Add(new()
            {
                Type = typeof(UIStackFrame),
                Name = "Threads stacks",
                Populate = a => a.Stacks()
                                .Select(s => new UIStackFrame()
                                {
                                    Thread = s.thread,
                                    StackFrames = s.stackFrames.ToList(),
                                })
                                .ToList(),
                Filter = (o, f) => ((UIStackFrame)o)?.Thread?.Address.ToString("x")?.FilterBy(f)
            });

            queries.Add(new()
            {
                Type = typeof(IClrRoot),
                Name = "Roots",
                Populate = a => a.Roots.ToList(),
                Filter = (o, f) => ((IClrRoot)o).Object.Type?.Name?.FilterBy(f)
            });

            queries.Add(new()
            {
                Type = typeof(ClrObject),
                Name = "ObjectsBySize",
                Populate = a => a.GetObjectsBySize(1).ToList(),
                Filter = (o, f) => ((ClrObject)o).Type?.Name?.FilterBy(f)
            });

            queries.Add(new()
            {
                Type = typeof(ClrObject),
                Name = "NonSystemObjectsBySize",
                Populate = a => a.GetObjectsBySize(1)
                                .Where(o => ((o.Type.Name != null &&
                                            !o.Type.Name.StartsWith("System") &&
                                            !o.Type.Name.StartsWith("Microsoft") &&
                                            !o.Type.Name.StartsWith("Interop") &&
                                            !o.Type.Name.StartsWith("Internal")) &&
                                            !o.Type.IsFree)
                                            || o.Type.Name == null)
                                .ToList(),
                Filter = (o, f) => ((ClrObject)o).Type?.Name?.FilterBy(f)
            });

            queries.Add(new()
            {
                Type = typeof(UIAllocatorGroup),
                Name = "GetObjectsGroupedByAllocator (.NET5+ dumps)",
                Populate = a => a.GetObjectsGroupedByAllocator(a.Objects)
                                .Select(g => new UIAllocatorGroup()
                                {
                                    Allocator = g.allocator,
                                    Objects = g.objects,
                                    Name = a.GetAllocatorName(g.allocator),
                                })
                                .ToList(),
                Filter = (o, f) => ((UIAllocatorGroup)o)?.Name?.FilterBy(f)
            });

            //queries.Add(new()
            //{
            //});

            return queries;
        }
    }
}
