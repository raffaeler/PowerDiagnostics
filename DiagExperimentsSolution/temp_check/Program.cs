using System;
using System.Reflection;
using Microsoft.Diagnostics.Runtime;

class Program
{
    static void Main()
    {
        var t = typeof(ClrRoot);
        Console.WriteLine("IsAbstract: " + t.IsAbstract);
        Console.WriteLine("IsSealed: " + t.IsSealed);
        foreach (var c in t.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            Console.WriteLine("ClrRoot ctor: " + c.ToString() + " IsPublic: " + c.IsPublic + " IsAssembly: " + c.IsAssembly);
        }
        foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            Console.WriteLine($"Property: {p.Name}, CanWrite: {p.CanWrite}");
        }

        var ct = typeof(ClrObject);
        Console.WriteLine("ClrObject IsValueType: " + ct.IsValueType);
        foreach (var c in ct.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            Console.WriteLine("ClrObject ctor: " + c.ToString() + " IsPublic: " + c.IsPublic + " IsAssembly: " + c.IsAssembly + " IsFamilyOrAssembly: " + c.IsFamilyOrAssembly);
        }

        var clt = typeof(GCRoot.ChainLink);
        Console.WriteLine("ChainLink IsClass: " + clt.IsClass);
        foreach (var c in clt.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            Console.WriteLine("ChainLink ctor: " + c.ToString() + " IsPublic: " + c.IsPublic + " IsAssembly: " + c.IsAssembly + " IsFamilyOrAssembly: " + c.IsFamilyOrAssembly);
        }
        foreach (var p in clt.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            Console.WriteLine($"ChainLink Property: {p.Name}, CanWrite: {p.CanWrite}");
        }
    }
}
