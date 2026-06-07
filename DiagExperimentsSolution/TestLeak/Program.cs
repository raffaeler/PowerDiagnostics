using System.Diagnostics;
using System.Text;

namespace TestLeak;

internal class Program
{
    static void Main(string[] args)
    {
        var process = Process.GetCurrentProcess();
        var pid = process.Id;
        process.Dispose();
        process = null;

        Console.WriteLine($"Process: {pid}");
        Console.WriteLine("App leaking some memory in a static field");
        var data = new SomeData();
        data.Fill();
        HoldingClass.Add(data);

        Console.WriteLine("Take the dump now");
        Console.ReadKey();
    }
}

public class SomeData
{
    private List<byte[]> _blobs = new List<byte[]>();
    public void Fill()
    {
        _blobs.Add(Encoding.UTF8.GetBytes("Some leaked data"));
        _blobs.Add(Encoding.UTF8.GetBytes("Some other leaked data"));
    }
}

public class HoldingClass
{
    private static List<SomeData> _leaks = new();
    public static void Add(SomeData data)
    {
        _leaks.Add(data);
    }
}