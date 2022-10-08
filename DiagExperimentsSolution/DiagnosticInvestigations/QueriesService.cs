using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DiagnosticInvestigations.Helpers;
using DiagnosticModels;

using Microsoft.Diagnostics.Runtime;

namespace DiagnosticInvestigations;

public class QueriesService
{
    public QueriesService()
    {
        var queries = CreateQueries();
        Queries = queries.ToDictionary(q => q.Name, q => q);
    }

    public IDictionary<string, KnownQuery> Queries { get; }
    private IList<KnownQuery> CreateQueries()
    {
        var queries = new List<KnownQuery>();

        queries.Add(new()
        {
            Type = typeof(DbmDumpHeapStat),
            Name = "DumpHeapStat",
            Populate = a => a.DumpHeapStat(0)
                            .Select(t => new DbmDumpHeapStat()
                            {
                                Type = t.type,
                                Objects = t.objects,
                                GraphSize = t.size,
                            })
                            .ToList(),
            Filter = (o, f) => ((DbmDumpHeapStat)o)?.Type?.Name?.FilterBy(f),
        });

        queries.Add(new()
        {
            Type = typeof(DbmStaticFields),
            Name = "GetStaticFieldsWithGraphAndSize",
            Populate = a => a.GetStaticFieldsWithGraphAndSize()
                            .Select(t => new DbmStaticFields()
                            {
                                Field = t.field,
                                Obj = t.obj,
                                Size = (long)t.size,
                            })
                            .ToList(),
            Filter = (o, f) => ((DbmStaticFields)o)?.Obj.Type?.Name?.FilterBy(f)
        });

        queries.Add(new()
        {
            Type = typeof(DbmDupStrings),
            Name = "GetDuplicateStrings",
            Populate = a => a.GetDuplicateStrings()
                            .Select(t => new DbmDupStrings()
                            {
                                Text = t.Key,
                                Count = t.Value,
                            })
                            .ToList(),
            Filter = (o, f) => ((DbmDupStrings)o)?.Text?.FilterBy(f)
        });

        queries.Add(new()
        {
            Type = typeof(DbmStringsBySize),
            Name = "GetStringsBySize",
            Populate = a => a.GetStringsBySize(0)
                            .Select(t => new DbmStringsBySize()
                            {
                                Obj = t.obj,
                                Text = t.text,
                            })
                            .ToList(),
            Filter = (o, f) => ((DbmStringsBySize)o)?.Text?.FilterBy(f),
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
            Type = typeof(DbmStackFrame),
            Name = "Threads stacks",
            Populate = a => a.Stacks()
                            .Select(s => new DbmStackFrame()
                            {
                                Thread = s.thread,
                                StackFrames = s.stackFrames.ToList(),
                            })
                            .ToList(),
            Filter = (o, f) => ((DbmStackFrame)o)?.Thread?.Address.ToString("x")?.FilterBy(f)
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
                            .Where(o => o.Type?.Name != null &&
                                        !o.Type.Name.StartsWith("System") &&
                                        !o.Type.Name.StartsWith("Microsoft") &&
                                        !o.Type.Name.StartsWith("Interop") &&
                                        !o.Type.Name.StartsWith("Internal") &&
                                        !o.Type.IsFree
                                        || o.Type?.Name == null)
                            .ToList(),
            Filter = (o, f) => ((ClrObject)o).Type?.Name?.FilterBy(f)
        });

        queries.Add(new()
        {
            Type = typeof(DbmAllocatorGroup),
            Name = "GetObjectsGroupedByAllocator (.NET5+ dumps)",
            Populate = a => a.GetObjectsGroupedByAllocator(a.Objects)
                            .Select(g => new DbmAllocatorGroup()
                            {
                                Allocator = g.allocator,
                                Objects = g.objects,
                                Name = a.GetAllocatorName(g.allocator),
                            })
                            .ToList(),
            Filter = (o, f) => ((DbmAllocatorGroup)o)?.Name?.FilterBy(f)
        });

        //queries.Add(new()
        //{
        //});

        return queries;
    }
}
