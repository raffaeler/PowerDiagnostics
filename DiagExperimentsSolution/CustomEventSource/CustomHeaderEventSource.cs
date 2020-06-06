using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Text;
using System.Threading;

namespace CustomEventSource
{
    public static class Constants
    {
        public const string CustomHeaderEventSourceName = "Raf.CustomHeader";
        public const string TriggerHeaderName = "X-TriggerHeaderEventSource";
        public const string TriggerHeaderCounter = "TriggerHeader";
    }

    [EventSource(Name = Constants.CustomHeaderEventSourceName)]
    public class CustomHeaderEventSource : EventSource
    {
        private long _triggerHeaderCounter;

        public CustomHeaderEventSource() :
            base(Constants.CustomHeaderEventSourceName,
                EventSourceSettings.EtwSelfDescribingEventFormat)
        {
        }

        public EventCounter TriggerHeader { get; private set; }

        public void RaiseTriggerHeaderCounter()
        {
            var nextValue = Interlocked.Increment(ref _triggerHeaderCounter);
            TriggerHeader.WriteMetric(nextValue);
        }

        protected override void OnEventCommand(EventCommandEventArgs command)
        {
            if (command.Command == EventCommand.Enable)
            {
                TriggerHeader = new EventCounter(Constants.TriggerHeaderCounter, this)
                {
                    DisplayName = "Count of the custom header received on any request",
                    DisplayUnits = "Num",
                };

            }

            //base.OnEventCommand(command);
        }
    }
}
