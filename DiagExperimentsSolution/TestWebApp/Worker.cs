using System;
using System.Collections.Generic;
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
            Debug.WriteLine(typeof(TestWebAddon.LeakyAddon).Name);
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            Debug.WriteLine("StartAsync");
            this._hostApplicationLifetime.ApplicationStopping.Register(OnApplicationStopping);

            _addonService.LoadContext();
            return base.StartAsync(cancellationToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            Debug.WriteLine("StopAsync");

            _addonService.UnloadContext();
            return base.StopAsync(cancellationToken);
        }

        private async void OnApplicationStopping()
        {
            Debug.WriteLine("OnApplicationStopping");
            await StopAsync(CancellationToken.None);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Factory.StartNew(() => DoingTheHardWork(stoppingToken), TaskCreationOptions.LongRunning);
            Debug.WriteLine("Exiting exeuction loop ...");
        }

        private void DoingTheHardWork(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (_generalConfig.EnableCpuStressInBackground)
                {
                    _cpuStressService.CpuLoad(_generalConfig.CpuStressMaxPrimeNumber, false);
                    _addonService.MakeAddonWork();
                }

                Thread.Sleep(2000);
            }
        }
    }
}
