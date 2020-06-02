using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace TestExceptions
{
    class Program
    {
        static Task Main(string[] args)
        {
            RunMenu();
            return Task.CompletedTask;
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
                        ThrowAndCatch();
                        break;

                    case ConsoleKey.D2:
                        ThrowAndCatchFilter();
                        break;

                    case ConsoleKey.D3:
                        ThrowAndCatchFinally();
                        break;

                    case ConsoleKey.D4:
                        ThrowInFinally();
                        break;

                    case ConsoleKey.C:
                        break;

                    default:
                        ignore = true;
                        break;
                }

                if (!ignore) Usage(keyInfo);
            }
            while (keyInfo.Key != ConsoleKey.Q);
        }

        private static void Usage(ConsoleKeyInfo key)
        {
            Console.Clear();
            Console.WriteLine($"Last Command: {key.KeyChar}");
            Console.WriteLine($"Pid = {Process.GetCurrentProcess().Id}");
            Console.WriteLine($"1. {nameof(ThrowAndCatch)}");
            Console.WriteLine($"2. {nameof(ThrowAndCatchFilter)}");
            Console.WriteLine($"3. {nameof(ThrowAndCatchFinally)}");
            Console.WriteLine($"4. {nameof(ThrowInFinally)}");
            Console.WriteLine($"C. Clear screen");
        }

        private static void ThrowAndCatch()
        {
            try
            {
                throw new ArgumentNullException("fake");
            }
            catch (Exception) { }
        }

        private static void ThrowAndCatchFilter()
        {
            try
            {
                throw new ArgumentNullException("fake");
            }
            catch (ArgumentNullException err) when (err.ParamName == "fake") { }
            catch (Exception) { }
        }

        private static void ThrowAndCatchFinally()
        {
            try
            {
                throw new ArgumentNullException("fake");
            }
            catch (ArgumentNullException err) when (err.ParamName == "fake") { }
            catch (Exception) { }
            finally
            {
                Console.WriteLine("Inside a finally statement");
            }
        }

        private static void ThrowInFinally()
        {
            try
            {
                throw new ArgumentNullException("fake");
            }
            catch (ArgumentNullException err) when (err.ParamName == "fake") { }
            catch (Exception) { }
            finally
            {
                try
                {
                    throw new ArgumentNullException("InsideFinally");
                }
                catch (ArgumentNullException) { }
            }
        }

    }

    public class MyClass : IDisposable
    {
        public MyClass()
        {
            Console.WriteLine("MyClass constructor");
        }

        public void Dispose()
        {
            Console.WriteLine("MyClass Dispose");
        }
    }
}
