using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;

//
// This is an experimental tool to trace the assembly being loaded in a .NET 5 process
// The goal is to create a tool similar to fuslogvw (Fusion Log Viewer) that was available with .NET Framework SDK
// The reason for "Fusion" is because the loading process in .NET has always been called this way.
//
// Version 5 of .NET is mandatory
// The tool requires a modified version of the Microsoft.Diagnostics.NETCore.Client which I downloaded from GitHub
// https://github.com/dotnet/diagnostics/tree/master/src/Microsoft.Diagnostics.NETCore.Client
// An official tool will hopefully come soon
//

namespace Fusion
{
    class Program
    {
        private const string _debuggee = @"../../../../FusionDebuggee/bin/Debug/net5.0/FusionDebuggee.exe";
        private const string _diagPortEnvKey = "DOTNET_DiagnosticPorts";

        private ClrTraceEventParser _parser;

        static async Task<int> Main(string[] args)
        {
            var p = new Program();
            await p.RunRemoteServer();
            return 0;
        }

        public async Task RunRemoteServer()
        {
            var debuggee = Path.GetFullPath(_debuggee);
            var diagnosticPort = $"Fusion_{Process.GetCurrentProcess().Id}";

            // 1. Start the diagnostic server
            ReversedDiagnosticsServer srv = new ReversedDiagnosticsServer(diagnosticPort);
            srv.Start();

            // 2. Start accepting connections
            using CancellationTokenSource cancellation = new CancellationTokenSource(20000);
            var acceptTask = srv.AcceptAsync(cancellation.Token);

            // 3. Run the debuggee
            Console.WriteLine("Starting child process using Diagnostic Port over Pipe");
            ProcessStartInfo psi = new(debuggee);
            psi.CreateNoWindow = false;
            psi.EnvironmentVariables[_diagPortEnvKey] = diagnosticPort;
            var process = Process.Start(psi);

            // 4. Wait for the remote CLR to connect with us
            var endpoint = await acceptTask;
            Console.WriteLine($"Remote process {endpoint.ProcessId} connected with cookie: {endpoint.RuntimeInstanceCookie}");


            // 5. Use the endpoint to start the diagnostic client
            using var fusion = new FusionTrace(endpoint.Endpoint);
            fusion.Start(e => SubscribeDynamicEvents(e), s => SubscribeRuntimeEvents(s));


            while (Console.ReadKey().Key != ConsoleKey.Q) ;
        }

        public void SubscribeDynamicEvents(Microsoft.Diagnostics.Tracing.TraceEvent e)
        {
            //if (e.PayloadNames.Length > 0)
            //{
            //    Console.WriteLine();
            //    Console.WriteLine($"{e.EventName} {e.EventIndex} {e.FormattedMessage}");
            //    var dn = string.Join(", ", e.GetDynamicMemberNames().Select(n => n.ToString()));
            //    Console.WriteLine($"{dn}");
            //    foreach (var name in e.PayloadNames)
            //    {
            //        Console.WriteLine($"name: {name}");
            //    }
            //    Console.WriteLine();

            //    //var payloadContainer = e.PayloadValue(0) as IDictionary<string, object>;

            //    //if (payloadContainer == null)
            //    //    return null;

            //    //if (payloadContainer["Payload"] is IDictionary<string, object> payload)
            //    //    return payload;
            //}


            ////Console.WriteLine(e.Dump(true, false));

        }

        public void SubscribeRuntimeEvents(Microsoft.Diagnostics.Tracing.TraceEventSource s)
        {
            _parser = new ClrTraceEventParser(s);
            //_parser.LoaderAppDomainLoad += d => Console.WriteLine($"LoaderAppDomainLoad {d.AppDomainID}");
            //_parser.LoaderAppDomainUnload += d => Console.WriteLine($"LoaderAppDomainUnload {d.AppDomainID}");
            //_parser.LoaderAssemblyLoad += d => Console.WriteLine($"LoaderAssemblyLoad {d.FullyQualifiedAssemblyName}");
            //_parser.LoaderAssemblyUnload += d => Console.WriteLine($"LoaderAssemblyUnload {d.FullyQualifiedAssemblyName}");
            //_parser.LoaderDomainModuleLoad += d => Console.WriteLine($"LoaderDomainModuleLoad {d.ModuleNativePath}");
            //_parser.LoaderModuleDCStartV2 += d => Console.WriteLine($"LoaderModuleDCStartV2 {d.ModuleNativePath}");
            //_parser.LoaderModuleDCStopV2 += d => Console.WriteLine($"LoaderModuleDCStopV2 {d.ModuleNativePath}");
            //_parser.LoaderModuleLoad += d => Console.WriteLine($"LoaderModuleLoad {d.ModuleNativePath}");
            //_parser.LoaderModuleUnload += d => Console.WriteLine($"LoaderModuleUnload {d.ModuleNativePath}");
            //_parser.AssemblyLoaderAppDomainAssemblyResolveHandlerInvoked += d => Console.WriteLine($"AssemblyLoaderAppDomainAssemblyResolveHandlerInvoked {d.ResultAssemblyPath}");
            //_parser.AssemblyLoaderAssemblyLoadContextResolvingHandlerInvoked += d => Console.WriteLine($"AssemblyLoaderAssemblyLoadContextResolvingHandlerInvoked {d.ResultAssemblyPath}");
            //_parser.AssemblyLoaderAssemblyLoadFromResolveHandlerInvoked += d => Console.WriteLine($"AssemblyLoaderAssemblyLoadFromResolveHandlerInvoked {d.RequestingAssemblyPath}");
            //_parser.AssemblyLoaderKnownPathProbed += d => Console.WriteLine($"AssemblyLoaderKnownPathProbed {d.PathSource}");
            _parser.AssemblyLoaderResolutionAttempted += d =>
            {
                switch(d.Result)
                {
                    case ResolutionAttemptedResult.Success:
                        Console.WriteLine($"AssemblyLoaderResolutionAttempted {d.Result} {d.AssemblyLoadContext} {d.ResultAssemblyPath} ");
                        break;
                    //default:
                    //    Console.WriteLine($"AssemblyLoaderResolutionAttempted {d.Result} {d.AssemblyLoadContext} {d.AssemblyName}");
                    //    break;
                }
            };
            //_parser.AssemblyLoaderStart += d =>
            //{
            //    if (d.AssemblyLoadContext != "Default") Console.WriteLine($"AssemblyLoaderStart {d.AssemblyLoadContext} {d.AssemblyPath}");
            //};
            //_parser.AssemblyLoaderStop += d =>
            //{
            //    if (d.AssemblyLoadContext != "Default") Console.WriteLine($"AssemblyLoaderStop {d.AssemblyLoadContext} {d.AssemblyPath}");
            //};
            //_parser.MethodLoad += d => Console.WriteLine($"MethodLoad {d.MethodToken}");
            //_parser.MethodLoadVerbose += d => Console.WriteLine($"MethodLoadVerbose {d.MethodNamespace} {d.MethodName}");
            //_parser.TypeLoadStart += d => Console.WriteLine($"TypeLoadStart {d.TypeLoadStartID}");
            //_parser.TypeLoadStop += d => Console.WriteLine($"TypeLoadStop {d.TypeName}");
        }

    }
}
