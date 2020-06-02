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
            Console.Clear();

            await FastLeak();
            //await SlowLeak();
            //await CpuLoad();
        }

        private static Task CpuLoad()
        {
            var primes = new Primes();
            foreach (var prime in primes) Console.Write(".");
            return Task.CompletedTask;
        }

        private static async Task FastLeak()
        {
            byte[] blob;
            while(true)
            {
                for (int i = 0; i < 1000; i++)
                {
                    Console.Write(".");
                    blob = new byte[1024 * 1024 * i];
                    await Task.Delay(200);
                }
            }
        }

        private static async Task SlowLeak()
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
