using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Diagnostics.Runtime;
using ClrDiagnostics.Extensions;
using ClrDiagnostics.Models;

namespace ClrDiagnostics;
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

    /// <summary>
    /// Asynchronously enumerates static fields with graph and size, reporting progress.
    /// </summary>
    /// <param name="onProgress">Callback invoked with the count of items processed so far.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that resolves to the list of static fields with graph and size.</returns>
    public Task<List<(ClrStaticField field, ClrObject obj, UInt64 size, IEnumerable<ClrGraphNode> graph)>> GetStaticFieldsWithGraphAndSizeAsync(
        Action<int> onProgress,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var result = new List<(ClrStaticField field, ClrObject obj, UInt64 size, IEnumerable<ClrGraphNode> graph)>();
            var fields = _clrRuntime
                .GetConstructedTypeDefinitions(t => t.StaticFields.Length > 0)
                .SelectMany(t => t.StaticFields)
                .Where(f => f.IsObjectReference)
                .Select(f => (field: f, value: f.ReadObject(MainAppDomain)))
                .Where(t => !t.value.IsNull)
                .ToList();

            int progressCount = 0;
            foreach (var item in fields)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var size = item.value.GetGraphSize();
                var graph = ClrGraph.CreateForChildren(item.value);
                result.Add((item.field, item.value, size, graph));

                progressCount++;
                onProgress(progressCount);
            }

            return result.OrderByDescending(t => t.size).ToList();
        }, cancellationToken);
    }
}

