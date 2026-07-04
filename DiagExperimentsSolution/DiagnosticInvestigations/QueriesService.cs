using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClrDiagnostics.Models;
using DiagnosticInvestigations.Helpers;
using DiagnosticModels;

using Microsoft.Diagnostics.Runtime;

namespace DiagnosticInvestigations;

public class QueriesService
{
    public QueriesService()
    {
        var queries = CreateQueries();
        Queries = queries.ToDictionary(q => q.Name!, q => q);
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
                                TypeName = t.type?.Name,
                                MT = t.type?.MethodTable ?? 0,
                                Objects = t.objects,
                                GraphSize = t.size,
                            })
                            .ToList(),
            Filter = (o, f) => ((DbmDumpHeapStat)o)?.TypeName?.FilterBy(f),
            HasDetails = true,
            DetailType = typeof(ClrObject),
            DetailProperty = "Objects",
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
            PopulateAsync = async (a, onProgress, cancellationToken) =>
            {
                var list = await a.GetStaticFieldsWithGraphAndSizeAsync(onProgress, cancellationToken);
                return list.Select(t => new DbmStaticFields()
                {
                    Field = t.field,
                    Obj = t.obj,
                    Size = (long)t.size,
                }).ToList();
            },
            Filter = (o, f) => ((DbmStaticFields)o)?.Obj.Type?.Name?.FilterBy(f),
            HasDetails = true,
            DetailType = typeof(ClrObject),
            DetailProperty = "Obj",
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
            Type = typeof(ModuleDataLight),
            Name = "Modules",
            Populate = a => a.ExtractModules()
                            .Select(md => new ModuleDataLight()
                            {
                                AssemblyName = md.AssemblyName ?? string.Empty,
                                Name = Path.GetFileName(md.FileName) ?? md.AssemblyName ?? string.Empty,
                                Address = md.Module != null ? $"0x{md.Module.ImageBase:X16}" : string.Empty,
                                Size = md.FileSize,
                                IsDynamic = md.IsDynamic,
                                IsNative = md.IsNative,
                                FileName = md.FileName,
                            })
                            .ToList(),
            Filter = (o, f) => ((ModuleDataLight)o)?.Name?.FilterBy(f),
            HasDetails = true,
            DetailType = typeof(ModuleDataDetail),
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
            Filter = (o, f) => ((DbmStackFrame)o)?.Thread?.Address.ToString("x")?.FilterBy(f),
            HasDetails = true,
            DetailType = typeof(ClrStackFrame),
            DetailProperty = "StackFrames",
        });

        queries.Add(new()
        {
            Type = typeof(ClrRoot),
            Name = "Roots",
            Populate = a => a.Roots.ToList(),
            Filter = (o, f) => ((ClrRoot)o).Object.Type?.Name?.FilterBy(f)
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
            Filter = (o, f) => ((DbmAllocatorGroup)o)?.Name?.FilterBy(f),
            HasDetails = true,
            DetailType = typeof(ClrObject),
            DetailProperty = "Objects",
        });

        //queries.Add(new()
        //{
        //});

        return queries;
    }
}
