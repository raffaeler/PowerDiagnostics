using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TestWebApp.Models
{
    public class DemoException : Exception
    {
        public DemoException() : base()
        {
        }

        public DemoException(string message) : base(message)
        {
        }

        public DemoException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
