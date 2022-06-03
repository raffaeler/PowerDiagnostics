using System;
using System.Collections.Generic;
using System.Text;

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
//
//
// Microsoft.EntityFrameworkCore
// https://github.com/dotnet/efcore/blob/main/src/EFCore/Infrastructure/EntityFrameworkEventSource.cs
// https://docs.microsoft.com/en-us/ef/core/logging-events-diagnostics/event-counters?tabs=windows
// dotnet counters monitor -p <pid>  Microsoft.EntityFrameworkCore
// [Microsoft.EntityFrameworkCore]
//    Active DbContexts                                               1
//    Execution Strategy Operation Failures (Count / 1 sec)           0
//    Execution Strategy Operation Failures (Total)                   0
//    Optimistic Concurrency Failures (Count / 1 sec)                 0
//    Optimistic Concurrency Failures (Total)                         0
//    Queries(Count / 1 sec)                                          1
//    Queries(Total)                                                189
//    Query Cache Hit Rate (%)                                      100
//    SaveChanges(Count / 1 sec)                                      0
//    SaveChanges(Total)                                              0
//
//
// Microsoft.Data.SqlClient.EventSource
// https://github.com/dotnet/SqlClient/blob/main/src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/SqlClientEventSource.cs
// https://docs.microsoft.com/en-us/sql/connect/ado-net/event-counters?view=sql-server-ver16

// EventSource:
// https://github.com/dotnet/runtime/blob/master/src/libraries/System.Private.CoreLib/src/System/Diagnostics/Tracing/EventSource.cs

namespace ClrDiagnostics.Triggers
{
    public enum KnownProviderName
    {
        /// <summary>
        /// "Microsoft-Windows-DotNETRuntime"
        /// https://github.com/dotnet/runtime/blob/master/src/libraries/System.Private.CoreLib/src/System/Diagnostics/Tracing/NativeRuntimeEventSource.cs
        /// </summary>
        Microsoft_Windows_DotNETRuntime,

        /// <summary>
        /// "System.Runtime"
        /// https://github.com/dotnet/runtime/blob/master/src/libraries/System.Private.CoreLib/src/System/Diagnostics/Tracing/RuntimeEventSource.cs
        /// </summary>
        System_Runtime,

        Microsoft_DotNETCore_SampleProfiler,

        /// <summary>
        /// Microsoft-AspNetCore-Hosting
        /// https://github.com/aspnet/Hosting/blob/master/src/Microsoft.AspNetCore.Hosting/Internal/HostingEventSource.cs
        /// https://github.com/dotnet/aspnetcore/blob/master/src/Hosting/Hosting/src/Internal/HostingEventSource.cs
        /// </summary>
        Microsoft_AspNetCore_Hosting,
    }

    public static class KnownProviders
    {
        private static Dictionary<KnownProviderName, string> Map =
            new Dictionary<KnownProviderName, string>()
        {
            { KnownProviderName.Microsoft_Windows_DotNETRuntime, "Microsoft-Windows-DotNETRuntime" },
            { KnownProviderName.System_Runtime, "System.Runtime" },
            { KnownProviderName.Microsoft_DotNETCore_SampleProfiler, "Microsoft-DotNETCore-SampleProfiler" },
            { KnownProviderName.Microsoft_AspNetCore_Hosting, "Microsoft.AspNetCore.Hosting" },
        };

        public static bool TryGetName(KnownProviderName provider, out string providerName)
        {
            return Map.TryGetValue(provider, out providerName);
        }
    }

}
