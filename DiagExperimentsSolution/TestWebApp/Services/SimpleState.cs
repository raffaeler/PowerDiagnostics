using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TestWebApp.Services
{
    public class SimpleState
    {
        static int _counter = 0;
        public int Next()
        {
            return Interlocked.Increment(ref _counter);
        }
    }
}
