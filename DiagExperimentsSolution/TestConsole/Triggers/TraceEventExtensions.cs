using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.Diagnostics.Tracing;

using TestConsole.Triggers.Models;

namespace TestConsole.Triggers
{
    public static class TraceEventExtensions
    {
        public static IDictionary<string, object> GetPayload(this TraceEvent traceEvent)
        {
            if(traceEvent.PayloadNames.Length > 0)
            {
                var payloadContainer = traceEvent.PayloadValue(0) as IDictionary<string, object>;

                if (payloadContainer == null)
                    return null;

                if (payloadContainer["Payload"] is IDictionary<string, object> payload)
                    return payload;
            }

            return null;
        }

    }
}
