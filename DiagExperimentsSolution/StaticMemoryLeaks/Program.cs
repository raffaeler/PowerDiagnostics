using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace StaticMemoryLeaks
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine($"Pid: {Process.GetCurrentProcess().Id}");
            var textProcessor = new TextProcessor("DeBelloGallico_LiberI.txt");
            var cts = new CancellationTokenSource();
            Console.CursorVisible = false;
            var task = textProcessor.Process(cts.Token, count =>
            {
                Console.CursorLeft = 0;
                Console.CursorTop = 2;
                Console.Write($"{count}   ");
            });
            Console.ReadKey();
            cts.Cancel();
            await Task.Delay(200);
            Console.CursorVisible = true;
        }
    }
}
