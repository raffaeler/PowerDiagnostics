using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.Diagnostics.Tracing;

namespace ClrDiagnostics.Extensions;
public static class TraceEventExtensions
{
    public static IDictionary<string, object>? GetPayload(this TraceEvent traceEvent)
    {
        if(traceEvent.PayloadNames.Length > 0)
        {
            var payloadContainer = traceEvent.PayloadValue(0) as IDictionary<string, object>;

            if (payloadContainer == null)
                return null;

            // Some trace events wrap the payload in an outer "Payload" key
            // (e.g. EventCounter events from certain providers). If present,
            // unwrap and return the inner dictionary.
            if (payloadContainer.TryGetValue("Payload", out var inner) &&
                inner is IDictionary<string, object> innerPayload)
            {
                return innerPayload;
            }

            // No outer wrapper — the container itself is the event payload
            // (e.g. EventCounter events from System.Runtime).
            return payloadContainer;
        }

        return null;
    }

}

