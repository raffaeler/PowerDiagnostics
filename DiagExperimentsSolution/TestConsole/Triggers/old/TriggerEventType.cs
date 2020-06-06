using System;
using System.Collections.Generic;
using System.Text;

namespace TestConsole.Triggers
{
    public enum TriggerEventType
    {
        /// <summary>
        /// "Microsoft-Windows-DotNETRuntime"
        /// https://github.com/dotnet/runtime/blob/master/src/libraries/System.Private.CoreLib/src/System/Diagnostics/Tracing/NativeRuntimeEventSource.cs
        /// </summary>
        DotNetRuntime,

        /// <summary>
        /// "System.Runtime"
        /// </summary>
        SystemRuntime,

        /// <summary>
        /// "Microsoft-DotNETCore-SampleProfiler"
        /// </summary>
        SampleProfiler,

        /// <summary>
        /// Microsoft-AspNetCore-Hosting
        /// https://github.com/aspnet/Hosting/blob/master/src/Microsoft.AspNetCore.Hosting/Internal/HostingEventSource.cs
        /// https://github.com/dotnet/aspnetcore/blob/master/src/Hosting/Hosting/src/Internal/HostingEventSource.cs
        /// </summary>
        AspNetCoreHosting,
    }
}
