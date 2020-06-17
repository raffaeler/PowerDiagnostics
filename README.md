# Power Diagnostics
Experiments in .NET Core diagnostics using:
- [ClrMD](https://github.com/microsoft/clrmd) 
- [DiagnosticClient](https://github.com/dotnet/diagnostics/tree/master/src/Microsoft.Diagnostics.NETCore.Client)
- [Trace Event library](https://github.com/microsoft/perfview/blob/master/documentation/TraceEvent/TraceEventLibrary.md)

The StressTestWebApp is the only project using .NET 5.
There is no particular reason other than the Http Client is easier to use.
You can change the framework to .NET Core 3 and change the code so that it uses application/json as Content-Type.

These projects assume running on Windows 10 + x64 CPU. The code can easily be migrated to support the other platforms and architectures.

Projects:
- ClrDiagnostics is a library using the libraries cited above
- CustomEventSource is a library defining a custom Trace Event / Counter
- DiagnosticWPF is a simple WPF application running some queries over dumps or running processes
- StressTestWebApp is the client app concurently calling the TestWebApp application
- TestConsole is just a way to run some code for testing
- TestWebApp is an ASP.NET Core application exposing a Razor page and an API controller where 'bad things' happen

