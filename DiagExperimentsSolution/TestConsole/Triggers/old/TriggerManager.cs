using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;

namespace TestConsole.Triggers
{
    public class TriggerManager
    {
        private DiagnosticsClient _dc;
        private List<EventPipeProvider> _providers;
        private static Dictionary<TriggerEventType, string> _knownProviders = new Dictionary<TriggerEventType, string>()
        {
            { TriggerEventType.DotNetRuntime, "Microsoft-Windows-DotNETRuntime" },
            { TriggerEventType.SystemRuntime, "System.Runtime" },
            { TriggerEventType.SampleProfiler, "Microsoft-DotNETCore-SampleProfiler" },
            { TriggerEventType.AspNetCoreHosting, "Microsoft-AspNetCore-Hosting" },
        };

        // HttpConnectionsEventSource => Microsoft.AspNetCore.Http.Connections
        // Microsoft-Extensions-Logging
        // Microsoft-System-Net-Quic ==> NetEventSource 
        // Microsoft-System-Net-Http ==> NetEventSource 
        // Microsoft-System-Net-Http-WinHttpHandler ==> NetEventSource 
        // Microsoft-System-Net-Requests ==> NetEventSource 
        // Microsoft-System-Net-WebHeaderCollection ==> NetEventSource 
        // Microsoft-System-Net-WebSockets-Client ==> NetEventSource 
        // Microsoft-System-Net-HttpListener ==> NetEventSource 
        // Microsoft-AspNetCore-Server-Kestrel
        // Microsoft-System-Net-Http
        // Dotnet-dev-certs ==> CertificateManagerEventSource
        //
        // Microsoft-Extensions-DependencyInjection ==> DependencyInjectionEventSource
        // System.Collections.Concurrent.ConcurrentCollectionsEventSource => CDSCollectionETWBCLProvider 
        // System.Buffers.ArrayPoolEventSource ==> ArrayPoolEventSource
        // Microsoft-System-Net-Security ==> NetEventSource


        private TriggerManager(int processId)
        {
            _dc = new DiagnosticsClient(processId);
            _providers = new List<EventPipeProvider>();
            //_dc.
        }

        public static TriggerManager Open(int processId)
        {
            return new TriggerManager(processId);
        }

        public static IList<Process> GetPublishedProcesses()
        {
            var result = DiagnosticsClient.GetPublishedProcesses()
                .Select(id => Process.GetProcessById(id))
                .ToList();

            return result;
        }

        public static string GetKnownProvider(TriggerEventType triggerEventType)
        {
            if (!_knownProviders.TryGetValue(triggerEventType, out string result)) return null;
            return result;
        }

        public void AddProvider(string name, long flags, System.Diagnostics.Tracing.EventLevel verbosity
            = System.Diagnostics.Tracing.EventLevel.Informational)
        {
            //ClrTraceEventParser.Keywords.
            new EventPipeProvider(name, verbosity, flags);
        }



    }
}
