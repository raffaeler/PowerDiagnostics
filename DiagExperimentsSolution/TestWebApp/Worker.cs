using System;
using System.Diagnostics;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using TestWebAddonContract;

using TestWebApp.Configurations;
using TestWebApp.Helpers;
using TestWebApp.Services;

namespace TestWebApp
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly CpuStressService _cpuStressService;
        private readonly AddonService _addonService;
        private readonly GeneralConfig _generalConfig;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private AssemblyLoadContext _myLoadContext;
        private WeakReference _isLoadContextAlive;
        private ILeakyAddon _addon;

        public Worker(ILogger<Worker> logger,
            CpuStressService cpuStressService,
            AddonService addonService,
            IOptions<GeneralConfig> generalConfig,
            IHostApplicationLifetime hostApplicationLifetime)
        {
            this._logger = logger;
            _cpuStressService = cpuStressService;
            _addonService = addonService;
            _generalConfig = generalConfig.Value;
            this._hostApplicationLifetime = hostApplicationLifetime;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            Debug.WriteLine("StartAsync");
            this._hostApplicationLifetime.ApplicationStopping.Register(OnApplicationStopping);

            _addonService.LoadContext();

            //_myLoadContext = new AssemblyLoadContext("MyLoadContext", true);
            //_isLoadContextAlive = new WeakReference(_myLoadContext, true);

            ////var asmName = typeof(System.Text.Json.JsonSerializer).Assembly.GetName();
            //var asmName = new System.Reflection.AssemblyName("TestWebAddon");   // see xcopy in post-build action
            //var file = System.IO.Path.Combine(GetCurrentExecutablePath(), "TestWebAddon");
            //var asm = _myLoadContext.LoadFromAssemblyPath(file);
            //var type = asm.GetType("TestWebAddon.LeakyAddon");
            //_addon = (ILeakyAddon)Activator.CreateInstance(type);
            //_myLoadContext.Unloading += OnMyLoadContextUnloading;

            //ContextHelpers.PrintAllContexts("Contexts");

            return base.StartAsync(cancellationToken);
        }

        //private string GetCurrentExecutablePath()
        //{
        //    return new System.IO.FileInfo(typeof(Startup).Assembly.Location).DirectoryName;
        //}

        //private void OnMyLoadContextUnloading(AssemblyLoadContext obj)
        //{
        //    _logger.LogInformation("Secondary context unloaded");
        //}

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            Debug.WriteLine("StopAsync");
            _addonService.UnloadContext();
            //_myLoadContext.Unload();
            //_myLoadContext.Unloading -= OnMyLoadContextUnloading;
            //_myLoadContext = null;
            //// _isLoadContextAlive.IsAlive tells whether the load context is still alive
            return base.StopAsync(cancellationToken);
        }

        private async void OnApplicationStopping()
        {
            Debug.WriteLine("OnApplicationStopping");
            await StopAsync(CancellationToken.None);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await DoingTheHardWork(stoppingToken);
            Debug.WriteLine("Exiting exeuction loop ...");
        }

        private Task DoingTheHardWork(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                Debug.WriteLine("ExecuteAsync ...");
                if (_generalConfig.EnableCpuStressInBackground)
                {
                    _cpuStressService.CpuLoad(_generalConfig.CpuStressMaxPrimeNumber, false);
                    //_addon.LeakSomeMemory(20);
                    _addonService.MakeAddonWork();
                }

                Thread.Sleep(2000);
                //await Task.Delay(2000);
            }

            return Task.CompletedTask;
        }
    }
}
