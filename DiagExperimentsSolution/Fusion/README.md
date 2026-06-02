# Fusion — AssemblyLoadContext Tracing Tool

Fusion is an experimental tool that traces assembly loading events in .NET processes, similar to the classic **fuslogvw** (Fusion Log Viewer) from .NET Framework.

The tool uses a **reversed diagnostics server** to connect to a child process. The debuggee (FusionDebuggee) is launched with the `DOTNET_DiagnosticPorts` environment variable, causing the runtime to connect back to Fusion. This allows Fusion to trace assembly events from process startup.

## How It Works

1. **FusionDebuggee** is launched with `DOTNET_DiagnosticPorts=<port>`
2. The debuggee's runtime connects back to Fusion over the diagnostic port
3. Fusion uses `DiagnosticsClientConnector.FromDiagnosticPort()` to accept the connection
4. The connector's `Instance` property provides a `DiagnosticsClient`
5. `ClrTraceEventParser` subscribes to assembly loader events (e.g., `AssemblyLoaderResolutionAttempted`)

## Dependencies

- `Microsoft.Diagnostics.NETCore.Client` (NuGet v0.2.661903) — uses `DiagnosticsClientConnector` for reversed diagnostics
- `Microsoft.Diagnostics.Tracing.TraceEvent`

## Reversed Diagnostics

Fusion uses `DiagnosticsClientConnector.FromDiagnosticPort()`, the **public API** for reversed (listen-mode) diagnostics introduced in `Microsoft.Diagnostics.NETCore.Client`. This replaces the older (and now internal) `ReversedDiagnosticsServer` + `IpcEndpoint` pattern.

```csharp
// Modern approach using public API:
var connector = await DiagnosticsClientConnector.FromDiagnosticPort(
    diagnosticPort, cancellation.Token);
using var fusion = new FusionTrace(connector.Instance);
fusion.Start(...);
```

> **Note**: Prior to June 2026, this project used a locally-modified copy of the library because `DiagnosticsClient(IpcEndpoint)` was `internal`. The `DiagnosticsClientConnector` API now provides the same functionality through a fully public path.

## Running

```bash
cd DiagExperimentsSolution
dotnet run --project Fusion
```

Press `Q` to quit after tracing.
