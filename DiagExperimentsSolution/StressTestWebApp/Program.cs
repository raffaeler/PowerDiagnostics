using Microsoft.Extensions.DependencyInjection;

using Polly;
using Polly.Extensions.Http;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

// monitoring counters:
// dotnet-counters monitor -p <pid> --providers Microsoft.AspNetCore.Hosting System.Runtime


namespace StressTestWebApp
{
    class Program
    {
        private static string _address = "https://localhost:5001";
        private static int _concurrency = 1000;
        private ServiceProvider _serviceProvider;
        private string _postPayload;

        static async Task Main(string[] args)
        {
            await new Program().Start();
        }

        private Task Start()
        {
            Initialize();
            return RunMenu();
        }

        private void Initialize()
        {
            // generate a random 1K string payload of readable characters
            Random rnd = new Random();
            _postPayload = new string(Enumerable.Range(0, 1024)                
                .Select(n => (char)(rnd.Next(0, 96) + 32))
                .ToArray());

            var services = new ServiceCollection();
            services.AddHttpClient("stress-client", c =>
            {

                c.BaseAddress = new Uri(_address);
                c.DefaultRequestHeaders.Add("User-Agent", "Raf-DotNext Http Client");
                //c.DefaultRequestHeaders.Accept.Add("application/json");
                c.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue
                {
                    NoCache = true,
                    NoStore = true,
                    MaxAge = new TimeSpan(0),
                    MustRevalidate = true
                };
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                MaxConnectionsPerServer = 2000,
            })
            .AddPolicyHandler(policy =>
            {
                return HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.NotFound)
                .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                .WaitAndRetryAsync(3, retry => TimeSpan.FromSeconds(Math.Pow(2, retry)));
            })
            .AddTypedClient<TestWebAppClient>();

            _serviceProvider = services.BuildServiceProvider();
        }

        private async Task RunMenu()
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
                        await ConcurrentGet(_concurrency);
                        break;

                    case ConsoleKey.D2:
                        await ConcurrentPost(_concurrency);
                        break;

                    case ConsoleKey.D3:
                        await ConcurrentFailPost(_concurrency);
                        break;

                    case ConsoleKey.D4:
                        await ConcurrentLongPost(_concurrency);
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

        private void Usage(ConsoleKeyInfo key)
        {
            Console.Clear();
            Console.WriteLine($"Last Command: {key.KeyChar}");
            Console.WriteLine($"Pid = {Process.GetCurrentProcess().Id}");
            Console.WriteLine($"1. {nameof(ConcurrentGet)}");
            Console.WriteLine($"2. {nameof(ConcurrentPost)}");
            Console.WriteLine($"3. {nameof(ConcurrentFailPost)}");
            Console.WriteLine($"4. {nameof(ConcurrentLongPost)}");
            Console.WriteLine($"C. Clear screen");
        }

        private Task ConcurrentGet(int concurrentConnections)
        {
            var evt = new ManualResetEventSlim();
            var requests = Enumerable.Range(0, concurrentConnections)
                .Select(_ => PrepareGetRequest(evt, "Index"))
                .ToArray();

            Console.WriteLine("Requests ready to go. Press any key to run them");
            Console.ReadKey();
            Console.WriteLine("GO!");
            evt.Set();

            return Task.WhenAll(requests);
        }

        private Task<bool> PrepareGetRequest(ManualResetEventSlim evt,
            string relativeAddress)
        {
            return Task.Run<bool>(async () =>
            {
                var client = _serviceProvider.GetRequiredService<TestWebAppClient>();

                evt.Wait();

                var result = await client.GetPage(relativeAddress);
                if (result)
                    Console.Write(".");
                else
                    Console.Write("X");

                return result;
            });
        }

        private Task ConcurrentPost(int concurrentConnections)
        {
            var evt = new ManualResetEventSlim();
            var requests = Enumerable.Range(0, concurrentConnections)
                .Select(_ => PreparePostRequest(evt,
                "/api/Test/PostTest", _postPayload))
                .ToArray();

            Console.WriteLine("Requests ready to go. Press any key to run them");
            Console.ReadKey();
            Console.WriteLine("GO!");
            evt.Set();

            return Task.WhenAll(requests);
        }

        private Task ConcurrentFailPost(int concurrentConnections)
        {
            var evt = new ManualResetEventSlim();
            var requests = Enumerable.Range(0, concurrentConnections)
                .Select(_ => PreparePostRequest(evt,
                "/api/Test/PostFailTest", _postPayload))
                .ToArray();

            Console.WriteLine("Requests ready to go. Press any key to run them");
            Console.ReadKey();
            Console.WriteLine("GO!");
            evt.Set();

            return Task.WhenAll(requests);
        }

        private Task ConcurrentLongPost(int concurrentConnections)
        {
            var evt = new ManualResetEventSlim();
            var requests = Enumerable.Range(0, concurrentConnections)
                .Select(_ => PreparePostRequest(evt,
                "/api/Test/PostLongTest", _postPayload))
                .ToArray();

            Console.WriteLine("Requests ready to go. Press any key to run them");
            Console.ReadKey();
            Console.WriteLine("GO!");
            evt.Set();

            return Task.WhenAll(requests);
        }

        private Task<bool> PreparePostRequest(ManualResetEventSlim evt,
            string relativeAddress, string payload)
        {
            return Task.Run<bool>(async () =>
            {
                var client = _serviceProvider.GetRequiredService<TestWebAppClient>();

                evt.Wait();

                var result = await client.PostTest(relativeAddress, payload);
                if (result)
                    Console.Write(".");
                else
                    Console.Write("X");

                return result;
            });
        }

    }
}
