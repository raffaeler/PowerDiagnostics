# Power Diagnostics

The code in this repository is part of the demos I use in my talks about advanced diagnostics in .NET.
The main idea is to explain how to **automate** the diagnostics using a side process that monitors an application running in production and, under certain triggers, takes a snapshot and understands what's going wrong.

While classic debuggers are powerful general-purpose diagnostic tools, writing an application-specific tool is even more powerful because it knows what to monitor and how objects are shaped. This translates into more specific and more helpful information.

The code in this repository **does not implement any automated task**, but instead shows — with a WPF desktop UI or a React web UI — how the building blocks work so that anyone can write a tailor-made automation for their applications.

## Web app
The Web application is the most complete GUI for this diagnostic tool. It can be reached on either http or https (a valid certificate installation is needed). Some significant screenshots are provided here.

<img width="1160" height="496" alt="image" src="https://github.com/user-attachments/assets/d1a4cafa-db81-43a6-b805-d5bbcbcfd6aa" />

<img width="1162" height="664" alt="image" src="https://github.com/user-attachments/assets/42fbeb88-8259-441f-99ce-e395b02ddb1e" />

<img width="1145" height="719" alt="image" src="https://github.com/user-attachments/assets/f06bf1f1-d26c-4f31-a595-b811525b2124" />

<img width="1121" height="571" alt="image" src="https://github.com/user-attachments/assets/38dcdf45-62b5-478d-a306-621219d13e6f" />

<img width="1116" height="373" alt="image" src="https://github.com/user-attachments/assets/5331252c-ce76-422f-bc40-174a56f68f6d" />

<img width="1118" height="492" alt="image" src="https://github.com/user-attachments/assets/0b977be5-3763-4cc5-8f5f-1abf8ec63cd8" />



## WPF App
This is the older version of the GUI and does not implement the full features. It does not use the DiagnosticServer but directly references the libraries to create or analyze the snapshot and dumps.

![StressTestWebApp](_images/UI-App.png)

---

## Architecture at a Glance

```
┌──────────────────────────────────────────────────────┐
│                     UI Layer                          │
│  ┌─────────────────┐    ┌─────────────────────────┐  │
│  │  DiagnosticWPF   │    │   uidiag (React + MUI)  │  │
│  │  (WPF Desktop)   │    │   (Web Frontend)        │  │
│  └────────┬────────┘    └───────────┬─────────────┘  │
│           │                         │                 │
│           │              ┌──────────┘                 │
│           │              │ HTTP REST + SignalR        │
│           ▼              ▼                            │
│  ┌──────────────────────────────────────────────┐     │
│  │         DiagnosticServer (ASP.NET Core)       │     │
│  │    BackgroundService + SignalR Hub + REST API │     │
│  └──────────────────────┬───────────────────────┘     │
│                         │                             │
│  ┌──────────────────────┼───────────────────────┐     │
│  │    DiagnosticInvestigations (Queries, State)  │     │
│  └──────────────────────┼───────────────────────┘     │
│                         │                             │
│  ┌──────────────────────┼───────────────────────┐     │
│  │  ClrDiagnostics (Core Engine) + Triggers      │     │
│  └──────────────────────┼───────────────────────┘     │
│                         │                             │
│  ┌──────────────────────┼───────────────────────┐     │
│  │    ClrMD | DiagnosticClient | TraceEvent       │     │
│  └──────────────────────────────────────────────┘     │
│                                                       │
│  ┌──────────────────────────────────────────────┐     │
│  │   DiagnosticModels (Shared DTOs — used by all)│     │
│  └──────────────────────────────────────────────┘     │
└──────────────────────────────────────────────────────┘
```

> For the deep-dive architecture, see **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)**.

---

## Target Framework

All projects target **.NET 10** (some with Windows-specific TFMs for WPF).

---

## Main Dependencies

Experiments in .NET diagnostics using:
- **[ClrMD](https://github.com/microsoft/clrmd)** — The code equivalent to the "SOS" plugin used in WinDbg. Cross-platform managed heap inspection.
- **[DiagnosticClient](https://www.nuget.org/packages/Microsoft.Diagnostics.NETCore.Client/)** (`Microsoft.Diagnostics.NETCore.Client`) — IPC channel (named pipes) with the .NET runtime, used by `dotnet-dump`, `dotnet-trace`, and other diagnostics tools.
- **[TraceEvent](https://github.com/microsoft/perfview/blob/master/documentation/TraceEvent/TraceEventLibrary.md)** — Cross-platform event tracing (ETW/EventPipe): GC, CPU, HTTP requests, exceptions, custom EventSource providers.
- **[WpfHexEditorControl](https://github.com/abbaye/WpfHexEditorControl/)** — Hex viewer control used in the WPF demo app.

### Frontend Dependencies
- **React 19** + **React Router 7** — Web UI framework
- **MUI (Material UI) 9** + **MUI X Data Grid 9** — Component library
- **Zustand 5** — State management
- **@microsoft/signalr 10** — Real-time WebSocket communication
- **Vite 8** + **TypeScript 6** — Build tooling

---

## Projects Description

### Core Libraries

| Project | Role |
|---------|------|
| **ClrDiagnostics** | Core wrapper around ClrMD and DiagnosticClient. LINQ-style extension methods on ClrMD types. SOS-equivalent queries. Event-based trigger system for CPU, memory, exceptions, HTTP, and custom events. |
| **DiagnosticModels** | Shared data transfer objects (DTOs). `Dbm*` classes for heap analysis, `Evs*` classes for event metrics, JSON serialization converters for ClrMD types. |
| **DiagnosticInvestigations** | Query catalog (`KnownQuery`), investigation session scoping (`InvestigationScope`), global state tracking (`InvestigationState`), and configuration models. |

### User Interfaces

| Project | Role |
|---------|------|
| **DiagnosticWPF** | WPF desktop application — direct consumer of `ClrDiagnostics`. Opens dumps, monitors live processes, runs queries, views raw memory via hex viewer. Deliberately no MVVM (kept simple for demos). See **[docs/WPF-Functionality-Reference.md](docs/WPF-Functionality-Reference.md)** for the complete feature catalog. |
| **diagnostic-ui** (`uidiag`) | React 19 web frontend. Process picker, dump upload, 11 diagnostic queries, master-detail data grids, GC root path visualization, hex viewer, heap memory map, per-object address inspection, and real-time event monitoring via SignalR. |
| **DiagnosticServer** | ASP.NET Core backend. Hosts REST API (28+ endpoints), SignalR hub (`/diagnosticHub`), OpenAPI/Scalar UI, background worker (`DebuggingSessionService`), and serves the React production build from `wwwroot`. |

### Test Projects

| Project | What It Tests |
|---------|---------------|
| **ClrDiagnostics.Tests** | Core diagnostic engine — analyzer construction, SOS queries, extensions, triggers |
| **DiagnosticInvestigations.Tests** | Query catalog, investigation state, scope lifecycle, known query metadata |
| **DiagnosticModels.Tests** | Model serialization, JSON converters, event value models |

### Demo & Stress-Test Applications

| Project | Role |
|---------|------|
| **StressTestWebApp** | Console app with a menu that sends requests to `TestWebApp` — causes CPU stress, memory leaks, high request rates, and sends custom trigger headers. |
| **TestWebApp** | ASP.NET Core web app used as a diagnostic target. |
| **TestLeak** | Simple app that creates managed memory leaks for testing. |
| **TestConsole** | Minimal test harness for quick experiments. |
| **TestWebAddon** / **TestWebAddonContract** | AssemblyLoadContext (ALC) dynamic loading demo. |
| **Fusion** / **FusionDebuggee** | AssemblyLoadContext leak producer and target. |
| **CustomEventSource** | Custom ETW/EventSource provider (`Raf-CustomHeader`) — fires on custom HTTP header, used to demonstrate custom trigger scenarios. |

---

## Web UI Features

The React frontend (`uidiag`) provides a full diagnostic experience:

### Pages
| Page | Route | Purpose |
|------|-------|---------|
| **Home** | `/` | Process picker, session creation (snapshot/dump), real-time event bar |
| **Debug** | `/debug/:sessionId` | Query picker, master-detail data grid, GC root panel, filter bar |
| **Detail** | `/debug/:sessionId/detail/:queryName/:rowIndex` | Detail rows for a selected master row |
| **Address** | `/debug/:sessionId/address/:address` | Per-object inspection: hex dump, field layout, data owner, referencing objects |
| **Method Table** | `/debug/:sessionId/MethodTable/:mt` | All heap objects for a given MethodTable |
| **Memory Map** | `/debug/:sessionId/memorymap` | Heap segment visualization |

### Key Components
- **MasterDetailGrid** — MUI DataGrid with server-column-metadata (fallback to client-side registry), path-based value extraction, hex/numeric formatting, row filtering, and address-aware row click/double-click
- **GcRootPanel / GcRootTree** — GC root path visualization with progress bar (SignalR streaming), recursive tree display, and drill-down links
- **HexViewerDialog** — Raw memory hex viewer (16 bytes/row, offset, ASCII panel), with clickable reference addresses
- **EventsBar** — Real-time event chips (CPU, GC Allocation, Working Set, HTTP Req/s, Custom Header, Exceptions) via SignalR
- **DataOwnerPanel** — Shows the containing object for any heap address
- **MemoryMapPage** — Proportional segment bars showing heap layout

### Available Diagnostic Queries
| Query | Result Type | Has Details |
|-------|------------|-------------|
| DumpHeapStat | `DbmDumpHeapStat` | ✓ Objects |
| GetStaticFieldsWithGraphAndSize | `DbmStaticFields` | ✓ Graph |
| GetDuplicateStrings | `DbmDupStrings` | ✗ |
| GetStringsBySize | `DbmStringsBySize` | ✗ |
| Modules | `ClrModule` | ✗ |
| Threads stacks | `DbmStackFrame` | ✓ StackFrames |
| Roots | `ClrRoot` | ✗ |
| ObjectsBySize | `ClrObject` | ✗ |
| NonSystemObjectsBySize | `ClrObject` | ✗ |
| GetObjectsGroupedByAllocator | `DbmAllocatorGroup` | ✓ Objects |

---

## DiagnosticServer API

### Process & Session Management
| Verb | Endpoint | Purpose |
|------|----------|---------|
| `GET` | `/api/processes` | List all .NET processes |
| `POST` | `/api/processes/attach/{pid}` | Attach event triggers |
| `POST` | `/api/processes/detach` | Detach triggers |
| `POST` | `/api/processes/snapshot/{pid}` | Take snapshot → sessionId |
| `POST` | `/api/processes/dump/{pid}` | Create dump → sessionId |
| `GET` | `/api/sessions` | List active sessions |
| `GET` | `/api/sessions/dumps` | List dump files |
| `POST` | `/api/sessions/open-dump-path` | Open from server path |
| `POST` | `/api/sessions/open-dump` | Upload dump file |
| `DELETE` | `/api/sessions/{sessionId}` | Close session |

### Query Execution
| Verb | Endpoint | Purpose |
|------|----------|---------|
| `GET` | `/api/sessions/queries` | List available query names |
| `GET` | `/api/sessions/queries/metadata` | Column definitions per query |
| `POST` | `/api/sessions/{sessionId}/{queryName}` | Execute query (optional `?filter=`) |

### Heap Analysis & Object Inspection
| Verb | Endpoint | Purpose |
|------|----------|---------|
| `POST` | `/api/sessions/{sessionId}/address/{hex}` | Raw hex data for address |
| `POST` | `/api/sessions/{sessionId}/methodTable/{hex}` | Objects by MethodTable |
| `GET` | `/api/sessions/{sessionId}/memorymap` | Heap segment layout |
| `POST` | `/api/sessions/{sessionId}/memory/{hex}` | Raw memory read (optional `?length=`) |
| `POST` | `/api/sessions/{sessionId}/layout/{hex}` | Field layout for object |
| `POST` | `/api/sessions/{sessionId}/dataowner/{hex}` | Containing object |
| `POST` | `/api/sessions/{sessionId}/referencing/{hex}` | Objects referencing this address |
| `POST` | `/api/sessions/{sessionId}/addressinfo/{hex}` | Combined info (layout + dataowner + referencing) |

### GC Root Path Analysis
| Verb | Endpoint | Purpose |
|------|----------|---------|
| `POST` | `/api/sessions/{sessionId}/gcroot/{hex}` | Find GC root paths (upstream) |
| `POST` | `/api/sessions/{sessionId}/addresspath/{hex}` | Bi-directional paths through object |

> The GC root path system uses **Option 3** strategy (predicate with cached target) as detailed in **[docs/GCRoot-Migration-Options.md](docs/GCRoot-Migration-Options.md)**. It includes a .NET 10+ static field fallback when standard GC roots are unavailable.

### SignalR Hub (`/diagnosticHub`)
Events streamed server→client:
- `onEvs` — Real-time diagnostic events (CPU, GC, exceptions, HTTP, working set)
- `onSessionCreated` / `onSessionClosed` — Session lifecycle
- `onQueryProgress` / `onQueryComplete` — Query execution progress
- `onGcRootProgress` / `onGcRootComplete` — GC root computation progress
- `onAddressPathProgress` / `onAddressPathComplete` — Address path computation progress

### MCP Server (Streamable HTTP & stdio)

DiagnosticServer also functions as an **MCP (Model Context Protocol)** server, enabling AI agents — Claude Desktop, GitHub Copilot, Continue, Cursor, and others — to inspect .NET memory dumps through natural conversation.

#### Architecture

The MCP server exposes the same diagnostic capabilities as the REST API through a set of AI-friendly tools. All tools are registered at startup:

**Session tools** (always available):
| Tool | Description |
|------|-------------|
| `list_dumps` | List available dump files by name from the configured `DumpsFolder` |
| `open_dump` | Open a dump by name. Returns session metadata (target framework, heap object count) and a catalog of available diagnostic tools |
| `list_sessions` | List all active diagnostic sessions |
| `close_session` | Close a session and release resources |

**Query tools** (one per diagnostic query):
| Tool | Underlying Query | Description |
|------|-----------------|-------------|
| `query_heap_stat` | DumpHeapStat | Type-level heap statistics. **Start here** on any new dump. |
| `query_static_fields` | GetStaticFieldsWithGraphAndSize | Static field memory analysis |
| `query_duplicate_strings` | GetDuplicateStrings | Duplicate string instances |
| `query_strings_by_size` | GetStringsBySize | Largest strings by size |
| `query_modules` | Modules | All loaded assemblies |
| `query_thread_stacks` | Threads stacks | Managed thread call stacks |
| `query_roots` | Roots | All GC roots |
| `query_objects_by_size` | ObjectsBySize | Largest individual heap objects |
| `query_non_system_objects` | NonSystemObjectsBySize | Same excluding System.* types |
| `query_objects_by_allocator` | GetObjectsGroupedByAllocator | Objects grouped by allocating method (.NET5+) |

**Inspection tools**:
| Tool | Description |
|------|-------------|
| `get_query_detail` | Paginated detail rows for a query result |
| `inspect_object` | Field layout, data owner, and referencing objects for a heap address |
| `get_gc_roots` | GC root paths keeping an object alive |
| `get_memory_map` | GC heap segment layout with per-generation summary |

#### Partial Results & Pagination

All query tools return paginated results to avoid overwhelming the AI's context window:
- **Default page size**: 20 items (configurable, max 100)
- **GC root paths**: default 3 paths (configurable, max 10)
- **Insights**: every response includes a `summary`, `topConsumers` list, `anomalies` detected, and `recommendedActions`

#### Configuration (appsettings.json)

```json
{
    "General": {
        "DumpsFolder": "H:\\_dumps"
    },
    "Mcp": {
        "DefaultPageSize": 20,
        "MaxPageSize": 100,
        "DefaultMaxPaths": 3,
        "MaxPaths": 10,
        "DefaultTopN": 50,
        "MaxInsightEntries": 5,
        "HttpEndpoint": "/mcp"
    }
}
```

#### Running as an MCP Server

**Streamable HTTP mode** (co-exists with REST API + Web UI):
```bash
cd DiagExperimentsSolution
dotnet run --project DiagnosticServer
# MCP endpoint available at http://localhost:5218/mcp
```

**stdio mode** (for desktop MCP clients):
```bash
cd DiagExperimentsSolution
dotnet run --project DiagnosticServer -- --stdio
```

#### MCP Client Configuration Examples

**Claude Desktop** (`claude_desktop_config.json`):
```json
{
    "mcpServers": {
        "powerdiagnostics": {
            "command": "dotnet",
            "args": ["run", "--project", "DiagExperimentsSolution/DiagnosticServer", "--", "--stdio"],
            "cwd": "/path/to/PowerDiagnostics"
        }
    }
}
```

**GitHub Copilot** (VS Code `.vscode/mcp.json`):
```json
{
    "servers": {
        "powerdiagnostics": {
            "type": "stdio",
            "command": "dotnet",
            "args": ["run", "--project", "DiagExperimentsSolution/DiagnosticServer", "--", "--stdio"],
            "cwd": "/path/to/PowerDiagnostics"
        }
    }
}
```

**Continue** (`config.json`):
```json
{
    "experimental": {
        "modelContextProtocolServers": [
            {
                "transport": {
                    "type": "stdio",
                    "command": "dotnet",
                    "args": ["run", "--project", "DiagExperimentsSolution/DiagnosticServer", "--", "--stdio"],
                    "cwd": "/path/to/PowerDiagnostics"
                }
            }
        ]
    }
}
```

**Cursor** (`.cursor/mcp.json`):
```json
{
    "mcpServers": {
        "powerdiagnostics": {
            "command": "dotnet",
            "args": ["run", "--project", "DiagExperimentsSolution/DiagnosticServer", "--", "--stdio"],
            "cwd": "/path/to/PowerDiagnostics"
        }
    }
}
```

**Streamable HTTP** (for remote/networked MCP clients):
```json
{
    "mcpServers": {
        "powerdiagnostics-http": {
            "type": "streamableHttp",
            "url": "http://localhost:5218/mcp"
        }
    }
}
```

---

## Build & Run

```bash
# Backend (from repo root)
cd DiagExperimentsSolution
dotnet build DiagExperimentsSolution.sln
dotnet run --project DiagnosticServer

# Run tests
dotnet test ClrDiagnostics.Tests
dotnet test DiagnosticInvestigations.Tests
dotnet test DiagnosticModels.Tests

# React frontend (development)
cd uidiag
npm install
npm run dev            # Vite dev server at localhost:3000 (proxies /api → localhost:5218)

# React frontend (production build → wwwroot)
npm run build          # output to ../DiagExperimentsSolution/DiagnosticServer/wwwroot

# After build, run DiagnosticServer to serve the full stack from a single process.
```

### Quick-Start Scripts
```bash
run.bat                # Build and run DiagnosticServer
run-wpf.bat            # Build and run DiagnosticWPF
run-test-apps.bat      # Launch StressTestWebApp + TestWebApp
```

---

## Web UI Debug Console

The `uidiag` React frontend includes a built-in debug utility accessible from the browser DevTools (F12). Use `__uidiag_debug.enable()` in the console to trace data flow, column resolution, and query execution.

```js
__uidiag_debug.enable()            // Enable all debug logging
__uidiag_debug.enable("grid,data") // Enable specific categories
__uidiag_debug.persist()           // Remember settings across reloads
__uidiag_debug.status()            // Check current state
```

**Categories**: `grid`, `data`, `signalr`, `query`, `api`, `store`

See **[docs/ARCHITECTURE.md §5.4](docs/ARCHITECTURE.md)** for full documentation.

---

## Documentation Index

| Document | Content |
|----------|---------|
| **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)** | Full architecture breakdown, layered design, patterns, data flow, communication mechanisms |
| **[docs/GCRoot-Migration-Options.md](docs/GCRoot-Migration-Options.md)** | GC root path strategies, ClrMD v2→v3 migration, .NET 10+ static field fallback |
| **[docs/WPF-Functionality-Reference.md](docs/WPF-Functionality-Reference.md)** | Complete WPF feature catalog (every button, grid, trigger, data flow) |

---

## Notes
These projects assume running on **Windows 10+ x64**. The core libraries (ClrMD, DiagnosticClient, TraceEvent) are cross-platform and can be migrated to Linux/macOS.

---

## Questions / Suggestions / Wishes
Feel free to open an issue :)

## Starring the Project
If you like the project, this would be a great way to tell it to me :)
