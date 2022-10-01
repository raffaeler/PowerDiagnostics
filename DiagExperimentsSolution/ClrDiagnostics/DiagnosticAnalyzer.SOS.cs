using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.Diagnostics.Runtime;
using ClrDiagnostics.Extensions;
using ClrDiagnostics.Models;
using System.Threading.Tasks;
using System.Threading;

namespace ClrDiagnostics
{
    /// <summary>
    /// This source file contains the methods emulating the SOS commands
    /// </summary>
    public partial class DiagnosticAnalyzer
    {
        /// <summary>
        /// equivalent to SOS dumpheap -stat
        /// </summary>
        public IEnumerable<(ClrType type, List<ClrObject> objects, long size)> DumpHeapStat(
            long minTotalSize = 1024)
        {
            return Objects
                .GroupBy(o => o.Type, o => o)
                .Select(o => (type: o.Key, objects: o.ToList(), totalSize: o.Sum(s => (long)s.Size)))
                .Where(t => t.totalSize > minTotalSize)
                .OrderBy(t => t.totalSize)
                .ThenBy(t => t.type.Name);
        }

        public void PrintDumpHeapStat(long minTotalSize = 1024)
        {
            var dumpHeapStat = DumpHeapStat(minTotalSize);
            var pinned = Roots.Where(r => r.IsPinned && r.Object.Type.Name == "System.Object[]");

            var pinnedType = pinned.FirstOrDefault()?.Object.Type;
            var pinnedSize = pinned.Sum(p => (long)p.Object.Size);
            var pinnedCount = pinned.Count();

            Console.WriteLine("              MT    Count    TotalSize Class Name");
            foreach (var t in dumpHeapStat)
            {
                Console.WriteLine($"{t.type.MethodTable:X16} {t.objects.Count,8} {t.size,12} {t.type.Name}");
            }

            var total = dumpHeapStat.Sum(d => d.objects.Count);
            Console.WriteLine($"Total {total} objects");
            Console.WriteLine();
            Console.WriteLine("Roots:");
            Console.WriteLine($"{pinnedType.MethodTable:X16} {pinnedCount,8} {pinnedSize,12} {pinnedType.Name}");
        }

        public Task<string> PrintRootsAsync(ClrObject clrObject, Action<int> onProgress, CancellationToken cancellationToken)
        {
            return Task.Run<string>(() => PrintRoots(clrObject, onProgress, cancellationToken));
        }

        public int GetGraphPathsCount(ClrObject clrObject)
        {
            int count = 0;
            var objectType = clrObject.Type;
            var roots = RootPaths(clrObject.Address);
            foreach (var root in roots)
            {
                count += root.Path.Length;
            }

            return count;
        }

        /// <summary>
        /// Create a long string with all the possible paths from the object to its roots
        /// </summary>
        /// <param name="clrObject">The object to analyze</param>
        /// <param name="onProgress">The progress of the operation. The total number is given by GetGraphPathsCount</param>
        /// <returns></returns>
        public string PrintRoots(ClrObject clrObject, Action<int> onProgress, CancellationToken cancellationToken)
        {
            StringBuilder sb = new StringBuilder();
            var objectType = clrObject.Type;
            sb.AppendLine($"{objectType.Name} Addr:0x{clrObject.Address:X} MT:0x{objectType.MethodTable:X} Size:{clrObject.Size}");
            sb.AppendLine();
            var roots = RootPaths(clrObject.Address);
            bool isFirst = true;
            int i = 0;
            int count = 0;
            foreach (var root in roots)
            {
                if (isFirst)
                {
                    sb.AppendLine($"Root {root.Root.RootKind} Addr:{root.Root.Address:X16} {root.Root.Object.Type.Name} ");
                    isFirst = false;
                }

                sb.AppendLine($"  Path {i++}");
                foreach (var path in root.Path)
                {
                    count++;
                    if (cancellationToken.IsCancellationRequested)
                        throw new OperationCanceledException("Canceled by user request");
                    onProgress(count);
                    sb.AppendLine($"     {path.Address:X16} {path.Type.Name}");
                    var result = FindReferencing(false, path.Address);
                    if (result != null && result.Count > 0)
                    {
                        sb.AppendLine($"          Objects whose fields point to {path.Address:X16}");
                        foreach (var res in result)
                        {
                            string isStaticString = res.isStatic ? "static" : "instance";
                            sb.AppendLine($"               {res.address:X16} Type:{res.typeName} [{isStaticString}] field:{res.fieldName}");
                        }
                    }

                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        private List<(ulong address, string typeName, string fieldName, bool isStatic)> FindReferencing(
            bool includeInstance, params ulong[] leakedAddresses)
        {
            List<(ulong address, string typeName, string fieldName, bool isStatic)> result = null;

            if (includeInstance)
            {
                foreach (var (obj, field, address) in ObjectsWithInstanceFields)
                {
                    if (leakedAddresses.Contains(address))
                    {
                        if (result == null)
                            result = new List<(ulong address, string typeName, string fieldName, bool isStatic)>();

                        result.Add((obj.Address, obj.Type.Name, field.Name, false));
                    }
                }
            }

            foreach (var (obj, field, address) in ObjectsWithStaticFields)
            {
                // returns zero if it is not an ulong
                if (leakedAddresses.Contains(address))
                {
                    if (result == null)
                        result = new List<(ulong address, string typeName, string fieldName, bool isStatic)>();

                    result.Add((obj.Address, obj.Type.Name, field.Name, true));
                }
            }

            return result;
        }


        public void PrintClrStack()
        {
            var stacks = this.Stacks();
            foreach (var stack in stacks)
            {
                Console.WriteLine($"OS Thread Id: 0x{stack.thread.OSThreadId:x} ({stack.thread.ManagedThreadId})");
                Console.WriteLine("        Child SP               IP Call Site");
                foreach (var frame in stack.stackFrames)
                {
                    var callSite = frame.FrameName == null
                        ? "" :
                        $"[{frame.FrameName} {frame.StackPointer:X16}]";


                    //var il = _dataTarget.DataReader.
                    //var x = frame.Method.Type.Module.
                    //var mi = frame.Method.Type.Module.MetadataImport;
                    //if (mi != null)
                    //{
                        
                    //}

                    Console.WriteLine($"{frame.StackPointer:X16} {frame.InstructionPointer:X16} {callSite} {frame.Method?.ToString()}");
                }
            }
        }

    }
}
