using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

using Polly;
using Polly.Extensions.Http;
using Microsoft.Extensions.DependencyInjection;

// monitoring counters:
// dotnet-counters monitor -p 23492 --providers Microsoft.AspNetCore.Hosting System.Runtime Raf-CustomHeader


namespace StressTestWebApp
{
    class Program
    {
        private static string _address = "https://localhost:5001";
        private static int _concurrency = 1000;
        private ServiceProvider _serviceProvider;

        static async Task Main(string[] args)
        {
            await new Program().Start();
        }

        private Task Start()
        {
            Initialize();
            var menu = new Menu(_serviceProvider, _address, _concurrency);
            return menu.Start();
        }

        private void Initialize()
        {
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

    }
}
