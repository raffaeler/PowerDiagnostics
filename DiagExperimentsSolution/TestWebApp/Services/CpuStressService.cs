using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using TestWebApp.Helpers;

namespace TestWebApp.Services
{
    public class CpuStressService
    {
        private readonly ILogger<CpuStressService> _logger;

        public CpuStressService(ILogger<CpuStressService> logger)
        {
            this._logger = logger;
        }

        public void CpuLoad(long max)
        {
            _logger.LogInformation($"Starting stressing the CPU on managed thread {Thread.CurrentThread.ManagedThreadId}");
            var primes = new Primes(max);
            Int64 sum = 0;
            foreach (var prime in primes)
            {
                sum += prime;
            }

            _logger.LogInformation($"CPU stressing completed on managed thread {Thread.CurrentThread.ManagedThreadId}");
            return;
        }


    }
}
