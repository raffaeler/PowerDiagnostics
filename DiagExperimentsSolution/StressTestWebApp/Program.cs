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

// Update the tool:
// dotnet tool update -g dotnet-counters
//
// Monitoring counters:
// dotnet-counters monitor -p 23492      --providers Microsoft.AspNetCore.Hosting System.Runtime Raf-CustomHeader
// dotnet-counters monitor -n TestWebApp --providers Microsoft.AspNetCore.Hosting System.Runtime Raf-CustomHeader
//
// Collecting a dump:
// dotnet-dump collect -o TestWebApp-GraphLeaks.dmp -p 1234

namespace StressTestWebApp
{
    class Program
    {
        private static string _address = "http://localhost:5200";
        private static int _concurrency = 1000;

        static async Task Main(string[] args)
        {
            var p = new Program();
            var serviceProvider = await p.Start();
            serviceProvider.Dispose();
        }

        private async Task<ServiceProvider> Start()
        {
            var serviceProvider = Initialize();
            var menu = new Menu(serviceProvider, _address, _concurrency);
            await menu.Start();
            return serviceProvider;
        }

        private ServiceProvider Initialize()
        {
            var services = new ServiceCollection();
            services.AddHttpClient("stress-client", c =>
            {

                c.BaseAddress = new Uri(_address);
                c.DefaultRequestHeaders.Add("User-Agent", "Raf Http Client");
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

            return services.BuildServiceProvider();
        }

    }
}
