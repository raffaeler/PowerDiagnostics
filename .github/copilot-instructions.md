# PowerDiagnostics Project Guidelines

## Overview
PowerDiagnostics is a .NET diagnostics demonstration project that shows how to automate production diagnostics using ClrMD, DiagnosticClient, and TraceEvent. It has a WPF desktop UI, a React web UI, and an ASP.NET Core backend — all targeting **.NET 6**.

## Code Style
- **Private fields**: `_camelCase` (underscore prefix) — e.g., `_dataTarget`, `_clrRuntime`
- **Public members**: `PascalCase`
- **File-scoped namespaces** preferred for new code: `namespace Foo.Bar;`
- **Partial classes** split by domain concern — see `ClrDiagnostics/DiagnosticAnalyzer.*.cs` for the canonical pattern
- **Extension methods** in `Extensions/` subfolder as `static` classes — e.g., `ClrObjectExtensions`, `TraceEventExtensions`
- **XML doc comments** (`/// <summary>`, `<param>`, `<returns>`) on all public APIs
- **`#region`** used *only* for the `Dispose` pattern, nowhere else
- **Using order**: `System.*` → `Microsoft.*` → third-party → project-local namespaces
- **All projects** must enable `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>` in their `.csproj`

## Architecture
| Project | Role |
|---------|------|
| **ClrDiagnostics** | Core wrapper: ClrMD heap analysis + DiagnosticClient IPC + LINQ query extensions |
| **DiagnosticModels** | Shared data models (`Dbm*` = heap stats, `Evs*` = event models) |
| **DiagnosticInvestigations** | Query definitions, investigation scopes, state tracking |
| **DiagnosticServer** | ASP.NET Core 6 hosting SignalR hub (`/diagnosticHub`) + diagnostics API + Swagger |
| **DiagnosticWPF** | WPF desktop UI — deliberately **no MVVM** (kept simple for demos) |
| **diagnostic-ui** | React 18 + Bootstrap 5 + `@microsoft/signalr` web frontend |
| **StressTestWebApp** / **TestConsole** / **TestWebApp** | Demo apps that generate diagnostic scenarios |
| **Fusion** / **FusionDebuggee** | AssemblyLoadContext leak demos |

## Build & Run
```bash
# Backend
cd DiagExperimentsSolution
dotnet build DiagExperimentsSolution.sln
dotnet run --project DiagnosticServer

# React frontend
cd diagnostic-ui
npm install
npm run start       # dev server at localhost:3000
npm run build       # production output
```

## Conventions
- **Documentation**: Detailed specifications, architecture decisions, and deep-dives go in `docs/`. See `docs/ARCHITECTURE.md` for the full architecture breakdown. Link from there rather than embedding here.
- **Extension methods over helpers**: Prefer fluent, chainable `static` extension methods on ClrMD types
- **Async patterns**: `CancellationToken` must be plumbed through all `async` methods; use `CancellationTokenSource` for cancellation
- **Threading**: Long-running diagnostics work runs on `ThreadPriority.BelowNormal` via `BackgroundService`
- **Testing**: Always create new tests for new functionalities. Test projects use the same conventions (net6.0, Nullable, ImplicitUsings)
- **Build & verify**: Always build and test the solution after modifying or adding any code
- **Docs & README**: Always keep `docs/` documentation and `README.md` up to date with code changes
- **Dispose**: Standard `Dispose()` → `GC.SuppressFinalize()` → `protected virtual Dispose(bool)` pattern

## Key References
- [ClrMD](https://github.com/microsoft/clrmd) — Managed heap inspection (SOS equivalent)
- [DiagnosticClient](https://github.com/dotnet/diagnostics) — IPC channel with the .NET runtime
- [TraceEvent](https://github.com/microsoft/perfview) — Cross-platform event tracing
- [WpfHexEditorControl](https://github.com/abbaye/WpfHexEditorControl) — Hex viewer control