using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

using DiagnosticModels;

using DiagnosticWPF.Helpers;
using Microsoft.Diagnostics.Runtime;

namespace DiagnosticWPF.Models
{
    public static class KnownGrids
    {
        private static Dictionary<Type, UIGrid> _store;
        static KnownGrids()
        {
            _store = new Dictionary<Type, UIGrid>();
            UIGrid g;

            g = UIGrid.Create<DbmDumpHeapStat>("Objects",
                    new UIGridColumn("Type", "Type", null, null, "Type", 300),
                    new UIGridColumn("MT", "Type.MethodTable", "0:X16", "MethodTable", null, DataGridLength.Auto),
                    new UIGridColumn("Graph Size", "GraphSize", "0:N0", null, "GraphSize", DataGridLength.Auto, true));
            _store[g.MasterType] = g;

            g = UIGrid.Create<ClrObject>(null,
                new UIGridColumn("Address", "Address", "0:X16", "Address", null, DataGridLength.Auto, true),
                new UIGridColumn("Size", "Size", "0:N0", null, "Size", DataGridLength.Auto, true),
                new UIGridColumn("Type", "Type", null, null, "Type", DataGridLength.Auto));
            _store[g.MasterType] = g;

            g = UIGrid.Create<ClrType>(null,
                new UIGridColumn("Name", "Name", null, "Name", null, 300),
                new UIGridColumn("IsFree", "IsFree", null, null, "IsFree", DataGridLength.Auto),
                new UIGridColumn("Module Name", "Module.Name", null, null, "Module.Name", DataGridLength.Auto));
            _store[g.MasterType] = g;

            g = UIGrid.Create<DbmStaticFields>(null,
                new UIGridColumn("Static field name", "Field.Name", null, null, "Field.Name", 200),
                new UIGridColumn("Size", "Size", "0:N0", null, "Size", DataGridLength.Auto, true),
                new UIGridColumn("Object", "Obj", null, null, "Obj", 300));
            _store[g.MasterType] = g;

            g = UIGrid.Create<DbmDupStrings>(null,
                new UIGridColumn("String", "Text", null, null, "Text", 200),
                new UIGridColumn("Count", "Count", "0:N0", null, "Count", DataGridLength.Auto, true));
            _store[g.MasterType] = g;

            g = UIGrid.Create<DbmStringsBySize>(null,
                new UIGridColumn("Object", "Obj", null, null, "Obj", 300),
                new UIGridColumn("String", "Text", null, null, "Text", 200),
                new UIGridColumn("Size", "Size", "0:N0", null, "Size", DataGridLength.Auto, true));
            _store[g.MasterType] = g;

            g = UIGrid.Create<ClrModule>(null,
                new UIGridColumn("AssemblyName", "AssemblyName", null, null, "AssemblyName", 300),
                new UIGridColumn("Name", "Name", null, null, "Name", 300),
                new UIGridColumn("Address", "Address", "0:X16", null, "Address", 200, true),
                new UIGridColumn("Size", "Size", "0:N0", null, "Size", DataGridLength.Auto, true));
            _store[g.MasterType] = g;

            g = UIGrid.Create<ClrThread>(null,
                new UIGridColumn("IsAlive", "IsAlive", null, null, "IsAlive", DataGridLength.Auto),
                new UIGridColumn("ManagedThreadId", "ManagedThreadId", null, null, "ManagedThreadId", 200),
                new UIGridColumn("Address", "Address", "0:X16", null, "Address", DataGridLength.Auto, true));
            _store[g.MasterType] = g;

            g = UIGrid.Create<DbmStackFrame>("StackFrames",
                new UIGridColumn("IsAlive", "Thread.IsAlive", null, null, "Thread.IsAlive", DataGridLength.Auto),
                new UIGridColumn("ManagedThreadId", "Thread.ManagedThreadId", null, null, "Thread.ManagedThreadId", 200),
                new UIGridColumn("Address", "Thread.Address", "0:X16", null, "Thread.Address", DataGridLength.Auto, true));
            _store[g.MasterType] = g;

            g = UIGrid.Create<ClrStackFrame>(null,
                new UIGridColumn("FrameName", "FrameName", null, null, "FrameName", 300),
                new UIGridColumn("Method", "Method", null, null, "Method", 300),
                new UIGridColumn("Kind", "Kind", null, null, "Kind", DataGridLength.Auto),
                new UIGridColumn("StackPointer", "StackPointer", "0:X16", null, "StackPointer", DataGridLength.Auto, true));
            _store[g.MasterType] = g;

            g = UIGrid.Create<IClrRoot>(null,
                new UIGridColumn("Address", "Address", "0:X16", null, "Address", DataGridLength.Auto, true),
                new UIGridColumn("Object", "Object", null, null, "Object", DataGridLength.Auto),
                new UIGridColumn("IsPinned", "IsPinned", null, null, "IsPinned", DataGridLength.Auto));
            _store[g.MasterType] = g;

            g = UIGrid.Create<DbmAllocatorGroup>("Objects",
                new UIGridColumn("Alloctor Address", "Allocator.Address", "0:X16", "Allocator.Address", null, DataGridLength.Auto, true),
                new UIGridColumn("Alloctor Size", "Allocator.Size", "0:N0", null, "Allocator.Size", DataGridLength.Auto, true),
                new UIGridColumn("Alloctor Type", "Allocator.Type", null, null, "Allocator.Type", DataGridLength.Auto),
                new UIGridColumn("Alloctor Name", "Name", null, null, "Name", DataGridLength.Auto));
            _store[g.MasterType] = g;
        }


        public static bool TryGetUIGridByType(Type type, out UIGrid uIGrid)
        {
            return _store.TryGetValue(type, out uIGrid);
        }

    }
}
