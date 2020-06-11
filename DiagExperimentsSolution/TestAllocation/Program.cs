using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace TestAllocation
{
    class Program
    {
        static void Main(string[] args)
        {
            RunMenu();
        }

        private static void RunMenu()
        {
            Usage(default(ConsoleKeyInfo));
            ConsoleKeyInfo keyInfo = default(ConsoleKeyInfo);
            do
            {
                keyInfo = Console.ReadKey(true);
                bool ignore = false;
                switch (keyInfo.Key)
                {
                    case ConsoleKey.D1:
                        AllocateArray();
                        break;

                    case ConsoleKey.D2:
                        FreeArrays();
                        break;

                    case ConsoleKey.D3:
                        AllocateSmallObject();
                        break;

                    case ConsoleKey.D4:
                        FreeSmallObject();
                        break;

                    case ConsoleKey.D5:
                        AllocateGraphRoots();
                        break;

                    case ConsoleKey.D6:
                        FreeGraphRoots();
                        break;

                    case ConsoleKey.G:
                        GC.Collect();
                        GC.Collect();
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        GC.Collect();
                        break;

                    case ConsoleKey.C:
                        break;

                    default:
                        ignore = true;
                        break;
                }

                if(!ignore) Usage(keyInfo);
            }
            while (keyInfo.Key != ConsoleKey.Q);
        }

        private static void Usage(ConsoleKeyInfo key)
        {
            Console.Clear();
            Console.WriteLine($"Last Command: {key.KeyChar}");
            Console.WriteLine($"Pid = {Process.GetCurrentProcess().Id}");
            Console.WriteLine($"1. {nameof(AllocateArray)}");
            Console.WriteLine($"2. {nameof(FreeArrays)}");
            Console.WriteLine($"3. {nameof(AllocateSmallObject)}");
            Console.WriteLine($"4. {nameof(FreeSmallObject)}");
            Console.WriteLine($"5. {nameof(AllocateGraphRoots)}");
            Console.WriteLine($"6. {nameof(FreeGraphRoots)}");
            Console.WriteLine($"G. GC.Collect");
            Console.WriteLine($"C. Clear screen");
        }

        static List<byte[]> _arrays = new List<byte[]>();
        private static void AllocateArray()
        {
            //_arrays.Add(new byte[10_000_000]);
            _arrays.Add(Alloc());
        }

        private static byte[] Alloc()
        {
            Random rnd = new Random();
            var length = 10_000_000;
            var blob = new byte[length];
            for (int i = 0; i < length; i++)
            {
                blob[i] = (byte)rnd.Next();
            }
            return blob;
        }

        private static void FreeArrays() => _arrays.Clear();

        static SmallObject _smallObject;
        private static void AllocateSmallObject() => _smallObject = new SmallObject();
        private static void FreeSmallObject() => _smallObject = null;


        static List<GraphRoot> _roots = new List<GraphRoot>();
        private static void AllocateGraphRoots()
        {
            for (int i = 0; i < 5; i++)
                _roots.Add(new GraphRoot());
        }
        private static void FreeGraphRoots() => _roots.Clear();

    }
}
