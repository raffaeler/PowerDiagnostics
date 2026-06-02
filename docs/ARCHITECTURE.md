# PowerDiagnostics — Architecture

This document describes the architecture, design decisions, and patterns used across the PowerDiagnostics solution. For coding conventions, see `.github/copilot-instructions.md`.

---

## 1. Solution Overview

PowerDiagnostics demonstrates how to **automate production diagnostics** for .NET applications. Rather than using a general-purpose debugger (like WinDbg), it shows how to build an application-specific diagnostic tool that knows what to monitor, which objects to inspect, and how to present results.

The solution targets **.NET 6** and runs on **Windows 10+ x64**, but is designed to be cross-platform in principle.

### Design Philosophy

| Principle | Implementation |
|-----------|---------------|
| **Application-specific diagnostics** | Custom queries that know the app's object model, not generic SOS commands |
| **Trigger-driven snapshots** | Event-based triggers (CPU, memory, exceptions, HTTP, custom) initiate analysis |
| **Separation of UI from diagnostics** | Core library is UI-agnostic; WPF and React both consume the same backend |
| **Fluent, LINQ-style APIs** | Extension methods on ClrMD types for composable, readable queries |
| **Demo-first, not production-hardened** | No MVVM, no test suite, no authentication — optimized for learning |

---

## 2. Project Map

```
PowerDiagnostics/
├── DiagExperimentsSolution/
│   ├── DiagExperimentsSolution.sln          # Solution file
│   │
│   ├── ClrDiagnostics/                      # ★ Core diagnostic engine
│   │   ├── DiagnosticAnalyzer.cs            #   Factory, lifecycle, IDisposable
│   │   ├── DiagnosticAnalyzer.Analysis.cs   #   Query methods
│   │   ├── DiagnosticAnalyzer.Allocators.cs #   Allocator-specific analysis
│   │   ├── DiagnosticAnalyzer.Experimental.cs
│   │   ├── DiagnosticAnalyzer.Graph.cs      #   Object graph traversal
│   │   ├── DiagnosticAnalyzer.SOS.cs        #   SOS-equivalent queries
│   │   ├── DiagnosticAnalyzer.Statics.cs    #   Static field analysis
│   │   ├── DiagnosticAnalyzer.Strings.cs    #   String dedup analysis
│   │   ├── Extensions/                      #   Extension methods on ClrMD types
│   │   ├── Helpers/                         #   Utility classes
│   │   ├── Models/                          #   Internal models (ClrGraph, etc.)
│   │   └── Triggers/                        #   Event-triggered snapshot logic
│   │
│   ├── DiagnosticModels/                    # ★ Shared data transfer objects
│   │   ├── Dbm*.cs                          #   Heap models (DbmDumpHeapStat, DbmAllocatorGroup, ...)
│   │   ├── Evs*.cs                          #   Event models (EvsCpu, EvsException, EvsGcAllocation, ...)
│   │   └── Converters/                      #   JSON serialization converters
│   │
│   ├── DiagnosticInvestigations/            # ★ Query catalog & investigation state
│   │   ├── KnownQuery.cs                    #   Enum of all available diagnostic queries
│   │   ├── InvestigationScope.cs            #   Snapshot context (dump/process/session)
│   │   ├── InvestigationKind.cs             #   Snapshot vs live vs dump
│   │   ├── InvestigationState.cs            #   Global state (connected clients, active queries)
│   │   ├── QueriesService.cs                #   Query execution & result materialization
│   │   ├── Configurations/                  #   App configuration models
│   │   └── Helpers/                         #   Investigation utilities
│   │
│   ├── DiagnosticServer/                    # ★ ASP.NET Core backend
│   │   ├── Program.cs                       #   Host setup, DI, middleware pipeline
│   │   ├── Hubs/DiagnosticHub.cs            #   SignalR hub for real-time push
│   │   ├── Controllers/                     #   REST API controllers
│   │   ├── Services/DebuggingSessionService.cs  # Background worker for diagnostics
│   │   ├── wwwroot/                         #   Served React production build
│   │   └── appsettings.json
│   │
│   ├── DiagnosticWPF/                       # WPF desktop UI
│   │   ├── MainWindow.xaml(.cs)             #   Main diagnostics view
│   │   ├── ProcessPicker.xaml(.cs)          #   Target process selection
│   │   ├── HexViewer.xaml(.cs)              #   Memory hex viewer
│   │   ├── Helpers/                         #   UI helpers
│   │   └── Models/                          #   View models (simple, not full MVVM)
│   │
│   ├── diagnostic-ui/                       # React web frontend
│   │   ├── src/                             #   React components & pages
│   │   └── public/                          #   Static assets
│   │
│   ├── StressTestWebApp/                    # Stress-test console (generates load)
│   ├── TestConsole/                         # Simple test harness
│   ├── TestWebApp/                          # Web app used as diagnostic target
│   ├── TestWebAddon/                        # Addon for ALC testing
│   ├── TestWebAddonContract/                # Shared contract for addon
│   ├── CustomEventSource/                   # Custom ETW/EventSource provider
│   ├── Fusion/                              # AssemblyLoadContext leak producer
│   ├── FusionDebuggee/                      # ALC leak target
│
├── docs/                                    # Documentation (this file)
│   └── ARCHITECTURE.md
│
└── .github/
    ├── copilot-instructions.md              # AI coding guidelines
    └── workflows/                           # CI/CD (if any)
```

---

## 3. Layered Architecture

```
┌─────────────────────────────────────────────┐
│              Presentation Layer              │
│  ┌──────────────┐  ┌─────────────────────┐  │
│  │ DiagnosticWPF│  │   diagnostic-ui      │  │
│  │  (WPF .NET)  │  │ (React 18 + SignalR) │  │
│  └──────┬───────┘  └──────────┬──────────┘  │
│         │                     │              │
│         │    ┌────────────────┘              │
│         │    │  HTTP + SignalR               │
│         ▼    ▼                               │
│  ┌──────────────────────────────────┐        │
│  │       Application Layer           │        │
│  │  ┌────────────────────────────┐  │        │
│  │  │    DiagnosticServer        │  │        │
│  │  │  (ASP.NET Core 6 + Swagger) │  │        │
│  │  │  ┌──────────────────────┐  │  │        │
│  │  │  │ DebuggingSessionSvc  │  │  │        │
│  │  │  │  (BackgroundService) │  │  │        │
│  │  │  └──────────┬───────────┘  │  │        │
│  │  └─────────────┼──────────────┘  │        │
│  └────────────────┼─────────────────┘        │
│                   │                          │
│  ┌────────────────┼─────────────────┐        │
│  │     Domain / Investigation Layer  │        │
│  │  ┌────────────────────────────┐  │        │
│  │  │  DiagnosticInvestigations  │  │        │
│  │  │  (Queries, Scopes, State)  │  │        │
│  │  └──────────────┬─────────────┘  │        │
│  └─────────────────┼────────────────┘        │
│                    │                         │
│  ┌─────────────────┼────────────────┐        │
│  │       Core Diagnostics Layer      │        │
│  │  ┌─────────────────────────────┐ │        │
│  │  │      ClrDiagnostics         │ │        │
│  │  │  ┌──────────┐ ┌──────────┐ │ │        │
│  │  │  │Diagnostic│ │ Triggers │ │ │        │
│  │  │  │Analyzer  │ │(CPU,Mem, │ │ │        │
│  │  │  │(ClrMD)   │ │Excpt,...)│ │ │        │
│  │  │  └──────────┘ └──────────┘ │ │        │
│  │  │  ┌──────────────────────┐  │ │        │
│  │  │  │ Extensions (LINQ)    │  │ │        │
│  │  │  │ • ClrObjectExt       │  │ │        │
│  │  │  │ • ClrTypeExt         │  │ │        │
│  │  │  │ • TraceEventExt      │  │ │        │
│  │  │  └──────────────────────┘  │ │        │
│  │  └─────────────────────────────┘ │        │
│  └──────────────────┬───────────────┘        │
│                     │                        │
│  ┌──────────────────┼───────────────┐        │
│  │     External Libraries           │        │
│  │  ┌───────────┐ ┌──────────────┐ │        │
│  │  │   ClrMD   │ │DiagnosticClient│ │       │
│  │  │(Heap insp)│ │   (IPC Pipes) │ │        │
│  │  └───────────┘ └──────────────┘ │        │
│  │  ┌───────────────────────────┐  │        │
│  │  │      TraceEvent           │  │        │
│  │  │   (Event/ETW Tracing)     │  │        │
│  │  └───────────────────────────┘  │        │
│  └─────────────────────────────────┘        │
│                                             │
│  ┌─────────────────────────────────┐        │
│  │     DiagnosticModels            │        │
│  │   (Shared DTOs — used by all)   │        │
│  └─────────────────────────────────┘        │
└─────────────────────────────────────────────┘
```

---

## 4. Core Architectural Patterns

### 4.1 Partial Class Pattern — `DiagnosticAnalyzer`

The central class `DiagnosticAnalyzer` is split across **8 partial files**, one per analysis domain:

| File | Responsibility |
|------|---------------|
| `DiagnosticAnalyzer.cs` | Lifecycle, factory methods (`FromDump`, `FromProcess`), `IDisposable`, caching |
| `DiagnosticAnalyzer.Analysis.cs` | Core heap query methods |
| `DiagnosticAnalyzer.Allocators.cs` | Memory allocator analysis |
| `DiagnosticAnalyzer.Experimental.cs` | Work-in-progress queries |
| `DiagnosticAnalyzer.Graph.cs` | Object graph creation and traversal |
| `DiagnosticAnalyzer.SOS.cs` | SOS-command-equivalent queries |
| `DiagnosticAnalyzer.Statics.cs` | Static field root analysis |
| `DiagnosticAnalyzer.Strings.cs` | String deduplication analysis |

**Rationale**: Each file represents a self-contained diagnostic domain. This keeps files manageable while allowing all methods to share `private` state (`_clrRuntime`, `_dataTarget`, caches).

### 4.2 Fluent Extension Methods (LINQ-Style)

Rather than helper classes, ClrMD types are extended with static methods for composable queries:

```csharp
// Typcial usage in DiagnosticAnalyzer.Statics.cs:
_clrRuntime
    .GetConstructedTypeDefinitions(t => t.StaticFields.Length > 0)
    .SelectMany(t => t.StaticFields)
    .Where(f => f.IsObjectReference)
    .Select(f => (field: f, value: f.ReadObject(MainAppDomain)))
    .Where(t => !t.value.IsNull);
```

Key extension classes:
- `ClrObjectExtensions` — Graph size, string extraction, null-safe operations
- `ClrTypeExtensions` — Type introspection helpers
- `TraceEventExtensions` — Event payload extraction

### 4.3 Trigger System

Triggers subscribe to TraceEvent providers and fire callbacks when conditions are met:

```
TriggerBase (abstract, IDisposable)
├── TriggerOnCpuLoad       — Fires on CPU threshold breach
├── TriggerOnExceptions    — Fires on CLR exception events
├── TriggerOnHttpRequests  — Fires on HTTP request events
├── TriggerOnMemoryUsage   — Fires on GC/working set events
└── TriggerOnEventCounter  — Fires on EventCounter metrics

TriggerAll — Composite that aggregates all triggers
```

Triggers are managed by `DebuggingSessionService`, which subscribes on session start and unsubscribes on disposal. When a trigger fires, it signals the worker thread (via `AutoResetEvent`) to take a snapshot and run queries.

### 4.4 Investigation System

```
InvestigationScope     — A single snapshot/dump context
├── SessionId          — Groups related scopes
├── InvestigationKind  — Snapshot | Dump | LiveProcess
├── DiagnosticAnalyzer — The analysis engine instance
└── TemporaryFile      — Temp dump file (if snapshot)

InvestigationState     — Global singleton tracking
├── ClientRefCount     — Connected SignalR clients
└── Active scopes      — Current investigation contexts

QueriesService         — Maps KnownQuery enum → DiagnosticAnalyzer methods
```

---

## 5. Data Flow

### 5.1 Real-Time Diagnostics Pipeline

```
┌──────────────┐    TraceEvent     ┌─────────────┐
│ Target .NET  │ ──── events ────▶ │  Triggers   │
│  Process     │                   │  (CPU,Mem,  │
│              │                   │   Exc,HTTP) │
└──────────────┘                   └──────┬──────┘
                                         │ AutoResetEvent.Set()
                                         ▼
                                  ┌─────────────────┐
                                  │ DebuggingSession │
                                  │    Service       │
                                  │  (BackgroundSvc) │
                                  └────────┬────────┘
                                           │
                          ┌────────────────┼────────────────┐
                          ▼                ▼                ▼
                   ┌──────────┐   ┌──────────────┐  ┌───────────┐
                   │ Take     │   │ Diagnostic   │  │ Run       │
                   │ Snapshot │──▶│ Analyzer     │─▶│ Queries   │
                   │ (dump)   │   │ (creates)    │  │           │
                   └──────────┘   └──────────────┘  └─────┬─────┘
                                                          │
                                                          ▼
                                                   ┌─────────────┐
                                                   │ Investigation│
                                                   │   Scope      │
                                                   │ + Results    │
                                                   └──────┬──────┘
                                                          │ SignalR
                                                          ▼
                                                   ┌─────────────┐
                                                   │  React Web  │
                                                   │     UI      │
                                                   └─────────────┘
```

### 5.2 WPF Desktop Flow

The WPF app is a **direct consumer** of `DiagnosticAnalyzer` — no server intermediary:

```
DiagnosticWPF → DiagnosticAnalyzer.FromDump(path)  // open dump file
DiagnosticWPF → DiagnosticAnalyzer.FromProcess(pid) // attach to process
              → analyzer.Queries...                // run queries directly
              → display in DataGrid + HexViewer
```

### 5.3 Web UI Flow

```
diagnostic-ui (React)
  │
  ├── HTTP REST ──▶ DiagnosticServer Controllers
  │                 (process list, query metadata)
  │
  └── SignalR ────▶ DiagnosticHub
                    (real-time query results, events, state changes)
```

---

## 6. Communication Mechanisms

| Mechanism | Library | Purpose |
|-----------|---------|---------|
| **DiagnosticClient IPC** | `Microsoft.Diagnostics.NETCore.Client` | Named pipe channel to .NET runtime for dump collection, EventPipe, etc. |
| **SignalR** | `Microsoft.AspNetCore.SignalR` | Real-time push of query results to the React UI at `/diagnosticHub` |
| **TraceEvent** | `Microsoft.Diagnostics.Tracing.TraceEvent` | Cross-platform event tracing (GC, CPU, HTTP, exceptions, custom EventSource) |
| **REST API** | ASP.NET Core Controllers | Process enumeration, query metadata, investigation management |

---

## 7. Key Design Decisions

### 7.1 No MVVM in WPF
The WPF app uses code-behind event handlers, not data binding/MVVM. This keeps the demo code accessible to developers unfamiliar with MVVM. View logic is deliberately simple.

### 7.2 No Automated Test Suite
Testing is entirely manual — via the `StressTestWebApp` console menu (generates load scenarios) and interactive use of the WPF/React UIs. This is a demo/educational project, not a production tool.

### 7.3 Lazy Caching in DiagnosticAnalyzer
Objects loaded from the CLR heap are cached on first access (`_cachedAllObjects`, `_objectsWithInstanceFields`, etc.) to avoid repeated expensive enumerations. Cache behavior is controlled by the `CacheAllObjects` flag set at construction.

### 7.4 Separate UI Tiers, Same Core
The WPF and React UIs are completely independent but both consume `ClrDiagnostics`. The WPF app uses it directly; the React app goes through `DiagnosticServer`. This demonstrates that the diagnostic engine is UI-agnostic.

### 7.5 CancellationToken Plumbing
All async diagnostic operations accept `CancellationToken` and use `CancellationTokenSource` internally. This allows cancellation of long-running heap traversals from the UI.

### 7.6 Thread Priority
Long-running diagnostic work runs on `ThreadPriority.BelowNormal` via the `BackgroundService` worker thread, preventing it from starving the main application thread.

---

## 8. Dependency Graph

```
DiagnosticModels ◄────────────────────────────────────────────┐
      ▲                                                        │
      │                                                        │
ClrDiagnostics ──── uses ────▶ Microsoft.Diagnostics.Runtime   │
      ▲                      (ClrMD)                            │
      │                      Microsoft.Diagnostics.NETCore      │
      │                      .Client (DiagnosticClient)         │
      │                      Microsoft.Diagnostics.Tracing      │
      │                      .TraceEvent                        │
      │                                                        │
DiagnosticInvestigations ◄──── depends ────────────────────────┘
      ▲
      │
DiagnosticServer ──── uses ────▶ DiagnosticInvestigations
      ▲                        DiagnosticModels
      │                        ClrDiagnostics
      │
DiagnosticWPF ──── uses ────▶ ClrDiagnostics
                            DiagnosticModels
```

---

## 9. Configuration

Configuration follows standard ASP.NET Core patterns:

```json
// appsettings.json — DiagnosticServer
{
  "General": {
    "ProcessId": 0,           // Target process (0 = pick interactively)
    "LoopTimeoutSeconds": 15  // Wait between diagnostic cycles
  }
}
```

The `GeneralConfiguration` class in `DiagnosticInvestigations/Configurations/` binds to this section via `IOptions<T>`.

---

## 10. Future Considerations

- **Cross-platform support**: Currently Windows-only; ClrMD and DiagnosticClient are cross-platform
- **Persistent storage**: Diagnostic results are ephemeral; a database would enable historical comparison
- **Authentication**: No auth on the SignalR hub or REST API
- **Reactive UI**: The WPF app could benefit from MVVM for more complex scenarios
- **Test automation**: Integration tests could validate queries against known dumps
