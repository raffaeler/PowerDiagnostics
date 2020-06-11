using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using TestWebApp.Helpers;

namespace TestWebApp.Services
{
    public class CpuStressService
    {
        public static Task CpuLoad()
        {
            var primes = new Primes();
            Int64 sum = 0;
            foreach (var prime in primes)
            {
                sum += prime;
            }

            return Task.CompletedTask;
        }


    }
}
