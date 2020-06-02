using System;
using System.Collections.Generic;
using System.Text;

namespace TestConsole.Triggers.Models
{
    public struct EventCountersPayload
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public double Mean { get; set; }
        public double StandardDeviation { get; set; }
        public double Count { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        public double IntervalSec { get; set; }
        public string Series { get; set; }
        public string CounterType { get; set; }
        public string Metadata { get; set; }
        public string DisplayUnits { get; set; }
    }
}
