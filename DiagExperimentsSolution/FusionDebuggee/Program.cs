using System;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.Loader;
using System.Xml.Linq;

namespace FusionDebuggee
{
    class Program
    {
        private static readonly string _assemblyName1 = "System.Xml.XDocument";
        private static readonly string _typeName = "System.Xml.Linq.XElement";

        static void Main(string[] args)
        {
            Console.WriteLine($"Env={Environment.GetEnvironmentVariable("DOTNET_DiagnosticPorts")}");
            Console.WriteLine("Starting ....  (press any key)");
            //Console.ReadKey();

            var xml = "<hello></hello>";
            Type type;
            object result;

            type = GetTypeFromContext(new MyContext());
            result = InvokeParse(type, xml);
            Console.WriteLine($"result (custom context): {result}");

            //type = GetTypeFromContext(AssemblyLoadContext.Default);
            //result = InvokeParse(type, xml);
            //Console.WriteLine($"result (default context): {result}");

            //Console.ReadKey();
        }

        private static Type GetTypeFromContext(AssemblyLoadContext ctx)
        {
            var asm = ctx.LoadFromAssemblyName(new AssemblyName(_assemblyName1));
            var type = asm.GetType(_typeName);
            return type;
        }

        private static object InvokeParse(Type xelementType, string xml)
        {
            var parseMethod = xelementType.GetMethod("Parse", new Type[] { typeof(string) });
            var result = parseMethod.Invoke(null, new object[] { xml });
            return result;
        }
    }

    public class MyContext : AssemblyLoadContext
    {
        private string _runtimeFolder;
        public MyContext()
            : base(name: "Custom-Collectible-MyContext", isCollectible: true)
        {
            AliveTracker = new WeakReference(this, true);
            _runtimeFolder = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
        }

        public WeakReference AliveTracker { get; }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            Assembly asm;
            // The base.Load always returns null
            //asm = base.Load(assemblyName);
            var file = Path.Combine(_runtimeFolder, assemblyName.Name + ".dll");
            if (!File.Exists(file)) return null;
            asm = base.LoadFromAssemblyPath(file);

            return asm;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            return base.LoadUnmanagedDll(unmanagedDllName);
        }

    }
}
