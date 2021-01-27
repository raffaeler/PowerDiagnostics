using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.Diagnostics.Runtime;
using ClrDiagnostics.Extensions;
using ClrDiagnostics.Models;
using System.Threading.Tasks;

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

        public Task<string> PrintRootsAsync(ClrObject clrObject)
        {
            return Task.Run<string>(() => PrintRoots(clrObject));
        }

        public string PrintRoots(ClrObject clrObject)
        {
            StringBuilder sb = new StringBuilder();
            var objectType = clrObject.Type;
            sb.AppendLine($"{objectType.Name} Addr:0x{clrObject.Address:X} Size:{clrObject.Size} MT:0x{objectType.MethodTable:X}");

            var roots = RootPaths(clrObject.Address);
            bool isFirst = true;
            int i = 0;
            foreach (var root in roots)
            {
                if (isFirst)
                {
                    sb.AppendLine($"Root {root.Root.RootKind} Addr:{root.Root.Address} {root.Root.Object.Type.Name} Addr:{root.Root.Address}");
                    isFirst = false;
                }

                sb.AppendLine($"  Path {i++}");
                foreach (var path in root.Path)
                {
                    sb.AppendLine($"     {path.Address:X16} {path.Type.Name}");
                    var result = FindReferencing(false, path.Address);
                    if (result.Count > 0)
                    {
                        sb.AppendLine($"                 Objects whose fields point to {path.Address:X16}");
                        foreach (var res in result)
                        {
                            string isStaticString = res.isStatic ? "static" : "instance";
                            sb.AppendLine($"                   {res.address:X16} Type:{res.typeName} field:{res.fieldName} {isStaticString}");
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
            var result = new List<(ulong address, string typeName, string fieldName, bool isStatic)>();

            foreach (var obj in Objects)
            {
                if (includeInstance)
                {
                    foreach (var instanceField in obj.Type.Fields.Where(f => f.IsObjectReference))
                    {
                        // returns zero if it is not an ulong
                        var staticFieldAddress = instanceField.Read<ulong>(obj.Address, false);
                        if (leakedAddresses.Contains(staticFieldAddress))
                        {
                            result.Add((obj.Address, obj.Type.Name, instanceField.Name, false));
                        }
                    }
                }

                foreach (var staticField in obj.Type.StaticFields.Where(f => f.IsObjectReference))
                {
                    // returns zero if it is not an ulong
                    var staticFieldAddress = staticField.Read<ulong>(MainAppDomain);
                    if (leakedAddresses.Contains(staticFieldAddress))
                    {
                        result.Add((obj.Address, obj.Type.Name, staticField.Name, true));
                    }
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
