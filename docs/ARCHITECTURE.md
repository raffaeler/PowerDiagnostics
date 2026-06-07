# PowerDiagnostics — Architecture

This document describes the architecture, design decisions, and patterns used across the PowerDiagnostics solution. For coding conventions, see `.github/copilot-instructions.md`.

---

## 1. Solution Overview

PowerDiagnostics demonstrates how to **automate production diagnostics** for .NET applications. Rather than using a general-purpose debugger (like WinDbg), it shows how to build an application-specific diagnostic tool that knows what to monitor, which objects to inspect, and how to present results.

The solution targets **.NET 10** and runs on **Windows 10+ x64**, but the core libraries (ClrMD, DiagnosticClient, TraceEvent) are cross-platform in principle.

### Design Philosophy

| Principle | Implementation |
|-----------|---------------|
| **Application-specific diagnostics** | Custom queries that know the app's object model, not generic SOS commands |
| **Trigger-driven snapshots** | Event-based triggers (CPU, memory, exceptions, HTTP, custom) initiate analysis |
| **Separation of UI from diagnostics** | Core library is UI-agnostic; WPF and React both consume the same backend |
| **Fluent, LINQ-style APIs** | Extension methods on ClrMD types for composable, readable queries |
| **Demo-first, education-optimized** | No authentication; code-behind in WPF; minimal API in server — optimized for learning |

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
│   │   ├── Extensions/                      #   Minimal API endpoint mapping
│   │   ├── Hubs/DiagnosticHub.cs            #   SignalR hub for real-time push
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
│   ├── diagnostic-ui/                     # React web frontend
│       ├── src/                             #   React components, pages, stores, services
│       │   ├── pages/                       #     HomePage, DebugPage, DetailPage,
│       │   │                               #     AddressPage, MethodTablePage, MemoryMapPage
│       │   ├── components/debug/            #     MasterDetailGrid, QueryPicker, GcRootPanel,
│       │   │                               #     GcRootTree, HexViewerDialog, EventsBar,
│       │   │                               #     FilterBar, DataOwnerPanel, SessionList
│       │   ├── components/home/            #     ProcessPicker, ProcessItem,
│       │   │                               #     SessionActions, DumpUploadDialog
│       │   ├── components/layout/          #     AppLayout, Header, Footer
│       │   ├── components/shared/          #     GenericDataGrid, HexViewer,
│       │   │                               #     JsonTree, ToastProvider
│       │   ├── stores/                     #     useDiagnosticsStore, useSignalRStore,
│       │   │                               #     useAppStore, useToastStore (Zustand 5)
│       │   ├── services/                   #     apiService, signalRService, authService
│       │   ├── types/                      #     api.ts, signalr.ts
│       │   ├── config/                     #     index.ts, gridRegistry.ts
│       │   └── utils/                      #     debug.ts, gridUtils.ts
│       └── public/                          #   Static assets
│   │
│   ├── StressTestWebApp/                    # Stress-test console (generates load)
│   ├── TestConsole/                         # Simple test harness
│   ├── TestWebApp/                          # Web app used as diagnostic target
│   ├── TestWebAddon/                        # Addon for ALC testing
│   ├── TestWebAddonContract/                # Shared contract for addon
│   ├── CustomEventSource/                   # Custom ETW/EventSource provider
│   ├── Fusion/                              # AssemblyLoadContext leak producer
│   ├── FusionDebuggee/                      # ALC leak target
│   ├── TestLeak/                            # Managed memory leak demo
│   │
│   ├── ClrDiagnostics.Tests/                # Unit tests: analyzer, SOS, extensions, triggers
│   ├── DiagnosticInvestigations.Tests/      # Unit tests: queries, scopes, state
│   └── DiagnosticModels.Tests/              # Unit tests: serialization, converters, models
│
├── docs/                                    # Documentation (this file)
│   ├── ARCHITECTURE.md
│   ├── GCRoot-Migration-Options.md
│   └── WPF-Functionality-Reference.md
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
│  │  (WPF .NET)  │  │ (React 19 + SignalR) │  │
│  └──────┬───────┘  └──────────┬──────────┘  │
│         │                     │              │
│         │    ┌────────────────┘              │
│         │    │  HTTP + SignalR               │
│         ▼    ▼                               │
│  ┌──────────────────────────────────┐        │
│  │       Application Layer           │        │
│  │  ┌────────────────────────────┐  │        │
│  │  │    DiagnosticServer        │  │        │
│  │  │  (ASP.NET Core 10 + Scalar)│  │        │
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
  ├── HTTP REST ──▶ DiagnosticServer Minimal API
  │                 (process list, query metadata, object inspection)
  │
  └── SignalR ────▶ DiagnosticHub
                    (real-time query results, events, state changes)
```

### 5.4 Browser Debug Console (`__uidiag_debug`)

The React frontend includes a debug utility exposed on `window.__uidiag_debug`. Open the browser DevTools (F12) and use these commands to trace data flow through the UI:

```js
// Enable all debug logging
__uidiag_debug.enable()

// Disable all debug logging
__uidiag_debug.disable()

// Quick toggle on/off
__uidiag_debug.toggle()

// Enable only specific categories (grid, data, signalr, query, api, store)
__uidiag_debug.enable("grid,data")

// Set verbosity level (0=off, 1=error, 2=warn, 3=info, 4=debug, 5=trace)
__uidiag_debug.level(5)

// Check current status
__uidiag_debug.status()

// List all available categories
__uidiag_debug.categories()

// Persist debug settings across page reloads (localStorage)
__uidiag_debug.persist()
__uidiag_debug.clearPersist()

// Dump first N rows of any data array to inspect shape
__uidiag_debug.dump(data, 3)
```

**Debug categories and what they trace:**

| Category | What's logged |
|----------|--------------|
| `grid` | Column definitions (server metadata vs client registry), column-to-row key matching, `valueGetter` resolution results for the first row |
| `data` | `queryResult` changes: row count, first-row keys and sample data |
| `query` | Query execution start/end, row count, first-row sampling, metadata fetch results with column paths |
| `api` | (reserved for future use) |
| `store` | (reserved for future use) |
| `signalr` | Real-time event parsing, state snapshots (see `useSignalRStore.ts`) |

**Common troubleshooting workflow:**

1. Open console, run `__uidiag_debug.enable()` then `__uidiag_debug.persist()`
2. Run a query from the UI
3. Look for `[QUERY]` logs showing the query result row count and first-row keys
4. Look for `[GRID]` logs showing which column source is used (server metadata vs client registry)
5. Look for `[GRID]` logs showing `column-to-row matching` — this reveals case mismatches between column `path` values and actual JSON property names on the row data

---

## 6. GC Root Path Analysis

### 6.1 Strategy: Predicate with Cached Target (Option 3)

The GC root path system uses a single `GCRoot` instance per target address, reconstructed only when the target changes. This follows **Option 3** from the detailed analysis in **[GCRoot-Migration-Options.md](GCRoot-Migration-Options.md)**:

```csharp
private GCRoot GetOrCreateGCRoot(ulong targetAddress)
{
    if (_gcroot == null || _gcrootTarget != targetAddress)
    {
        _gcrootTarget = targetAddress;
        _gcroot = new GCRoot(_clrRuntime.Heap, o => o.Address == targetAddress);
    }
    return _gcroot;
}
```

This achieves exact v2-equivalent filtering (one target at a time) with minimal allocations.

### 6.2 Static Field Fallback (.NET 10+)

In .NET 10+, some objects (particularly arrays) may not appear via standard GC root enumeration. The system includes a tiered fallback:

1. **Primary**: `GCRoot.EnumerateRootPaths()` — standard GC root paths
2. **Fallback 1**: `FindPathsToStatics()` — objects alive only via static fields
3. **Fallback 2**: `FindReferencing()` — scan for objects with instance/static field references to the target
4. **Fallback 3**: `TraceSingleChain()` / `FindArrayOwner()` — greedy single-chain trace for arrays

This is implemented in `DiagnosticAnalyzer.Analysis.cs` and `DiagnosticAnalyzerHelper.cs` (Experimental/).

### 6.3 API Endpoints

| Verb | Endpoint | Purpose |
|------|----------|---------|
| `POST` | `/api/sessions/{id}/gcroot/{addr}` | Find GC root paths (upstream: root → target) |
| `POST` | `/api/sessions/{id}/addresspath/{addr}` | Bi-directional path analysis (root → target → references) |

Both endpoints accept optional `?maxPaths={n}` and stream progress via SignalR (`onGcRootProgress`, `onGcRootComplete`, `onAddressPathProgress`, `onAddressPathComplete`).

### 6.4 Result Model

```
GcRootPathResult
├── Paths: GcRootPathNode[]
│   └── Children: GcRootPathNode[] (recursive)
│   └── ReferencingObjects: GcReferenceInfo[]
├── TotalPaths: int
└── TotalReferences: int
```

---

## 7. Communication Mechanisms

| Mechanism | Library | Purpose |
|-----------|---------|---------|
| **DiagnosticClient IPC** | `Microsoft.Diagnostics.NETCore.Client` | Named pipe channel to .NET runtime for dump collection, EventPipe, etc. |
| **SignalR** | `Microsoft.AspNetCore.SignalR` | Real-time push of query results and events to the React UI at `/diagnosticHub` |
| **TraceEvent** | `Microsoft.Diagnostics.Tracing.TraceEvent` | Cross-platform event tracing (GC, CPU, HTTP, exceptions, custom EventSource) |
| **REST API** | ASP.NET Core Minimal API | Process enumeration, query execution, object inspection, session management |

### 7.1 REST API Summary

The DiagnosticServer exposes **28+ endpoints** via minimal API (no controllers). For the complete reference, see the **README.md** API section. Key endpoint groups:

- **Process Management** (5): list, attach, detach, snapshot, dump
- **Session Management** (5): list, list dumps, open dump path, upload dump, close
- **Query Execution** (3): list queries, column metadata, run query
- **Heap Analysis** (4): address hex, methodTable objects, memory map, raw memory read
- **GC Root Analysis** (2): gcroot paths, address paths (bi-directional)
- **Object Inspection** (4): field layout, data owner, referencing objects, combined address info

### 7.2 SignalR Hub Events

**Server → Client:**

| Event | Payload | Trigger |
|-------|---------|---------|
| `onEvs` | `EvsEvent` JSON | Real-time trace events (CPU, GC, exceptions, HTTP, working set, custom header) |
| `onSessionCreated` | `{ sessionId, kind }` | New session created |
| `onSessionClosed` | `{ sessionId }` | Session closed/expired |
| `onQueryProgress` | `{ sessionId, queryName, count, status }` | Every 10 rows during query execution |
| `onQueryComplete` | `{ sessionId, queryName, rowCount }` | Query execution finished |
| `onGcRootProgress` | `{ sessionId, objectAddress, count, status }` | Every 100 chain links during GC root computation |
| `onGcRootComplete` | `{ sessionId, objectAddress, pathCount }` | GC root computation finished |
| `onAddressPathProgress` | `{ sessionId, objectAddress, count, status }` | Every 100 nodes during address path computation |
| `onAddressPathComplete` | `{ sessionId, objectAddress, pathCount }` | Address path computation finished |

**Client → Server:**

| Method | Arguments | Purpose |
|--------|-----------|---------|
| `SendMessage` | `(user, message)` | Chat-style messages |

---

## 8. Key Design Decisions

### 8.1 No MVVM in WPF
The WPF app uses code-behind event handlers, not data binding/MVVM. This keeps the demo code accessible to developers unfamiliar with MVVM. View logic is deliberately simple.

### 8.2 Test Infrastructure

The solution includes three test projects:
- **ClrDiagnostics.Tests** — Core engine tests: analyzer construction, SOS query output, extension methods, trigger subscriptions
- **DiagnosticInvestigations.Tests** — Query catalog tests: known query metadata, investigation state lifecycle, scope management
- **DiagnosticModels.Tests** — Model tests: serialization, JSON converters, event value models

Tests use **NSubstitute** for mocking, **FluentAssertions** for assertions, and **xUnit** as the test framework. All test projects target `net10.0` with `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>`.

### 8.3 Lazy Caching in DiagnosticAnalyzer
Objects loaded from the CLR heap are cached on first access (`_cachedAllObjects`, `_objectsWithInstanceFields`, etc.) to avoid repeated expensive enumerations. Cache behavior is controlled by the `CacheAllObjects` flag set at construction.

### 8.4 Separate UI Tiers, Same Core
The WPF and React UIs are completely independent but both consume `ClrDiagnostics`. The WPF app uses it directly; the React app goes through `DiagnosticServer`. This demonstrates that the diagnostic engine is UI-agnostic.

### 8.5 CancellationToken Plumbing
All async diagnostic operations accept `CancellationToken` and use `CancellationTokenSource` internally. This allows cancellation of long-running heap traversals from the UI.

### 8.6 Thread Priority
Long-running diagnostic work runs on `ThreadPriority.BelowNormal` via the `BackgroundService` worker thread, preventing it from starving the main application thread.

## 9. Dependency Graph

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

## 10. Configuration

Configuration follows standard ASP.NET Core patterns:

```json
// appsettings.json — DiagnosticServer
{
  "General": {
    "DebuggingSessionsExpirationMinutes": 1,
    "DumpsFolder": "H:\\_dumps"
  }
}
```

| Section | Key | Description |
|---------|-----|-------------|
| `General` | `DebuggingSessionsExpirationMinutes` | How long before an idle session is cleaned up |
| `General` | `DumpsFolder` | Server-side folder for dump files |

The `GeneralConfiguration` class in `DiagnosticInvestigations/Configurations/` binds to the `General` section via `IOptions<T>`.

---

## 11. Future Considerations

- **Cross-platform support**: Currently Windows-only; ClrMD and DiagnosticClient are cross-platform
- **Persistent storage**: Diagnostic results are ephemeral; a database would enable historical comparison
- **Authentication**: No auth on the SignalR hub or REST API
- **Reactive UI**: The WPF app could benefit from MVVM for more complex scenarios
- **Test expansion**: Add integration tests that validate queries against known dump files
