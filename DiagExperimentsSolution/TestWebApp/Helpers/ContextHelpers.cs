using System;
using System.Linq;
using System.Runtime.Loader;

namespace TestWebApp.Helpers
{
    public static class ContextHelpers
    {
        private static ConsoleColor DefaultColor = ConsoleColor.White;
        public static bool FilterOutSystemLibraries = false;

        public static void PrintAllContexts(string title, bool excludeDefault = true)
        {
            Console.ForegroundColor = DefaultColor;
            Console.WriteLine("\r\n-----");

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"{title}");
            Console.ForegroundColor = DefaultColor;
            foreach (var context in AssemblyLoadContext.All)
            {
                if (excludeDefault && context.Name == "Default")
                {
                    Console.WriteLine("Skipping default context");
                    continue;
                }

                Console.Write("Context: ");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write(context.Name);
                Console.ForegroundColor = DefaultColor;
                Console.WriteLine($" - #Assemblies:{context.Assemblies.Count()} - IsCollectible:{context.IsCollectible}");
                foreach (var asm in context.Assemblies)
                {
                    var fullName = asm.FullName;
                    if (FilterOutSystemLibraries && (fullName.StartsWith("System") || fullName.StartsWith("Microsoft")))
                        continue;

                    Console.WriteLine($"  - {fullName}");
                }
            }

            Console.WriteLine("-----\r\n");
        }

        public static void PrintAppDomain(string title)
        {
            Console.WriteLine("\r\n-----");

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"{title}");
            Console.ForegroundColor = DefaultColor;
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            Console.WriteLine($"AppDomain - #Assemblies:{assemblies.Length}");
            foreach (var asm in assemblies)
            {
                var fullName = asm.FullName;
                if (FilterOutSystemLibraries && (fullName.StartsWith("System") || fullName.StartsWith("Microsoft")))
                    continue;

                Console.WriteLine($"  - {fullName}");
            }
        }

    }
}
