# UIDiag — PowerDiagnostics React Frontend

TypeScript-based React frontend for PowerDiagnostics, built with Vite, Material UI (MUI), Zustand, and SignalR. Replaces the legacy `diagnostic-ui` with a maintainable, type-safe architecture.

## Tech Stack

| Technology | Version | Purpose |
|---|---|---|
| React | 19 | UI library |
| TypeScript | 6 | Type safety |
| Vite | 8 | Bundler & dev server |
| Material UI | 9 | Component library |
| MUI DataGrid | 9 | Master-detail data grids |
| Zustand | 5 | State management |
| React Router | 7 | Client-side routing |
| @microsoft/signalr | 10 | Real-time diagnostic events |

## Getting Started

```bash
npm install
npm run dev        # Dev server on http://localhost:3000
npm run build      # Production build -> DiagnosticServer/wwwroot/
```

The Vite dev server proxies `/api/*` and `/diagnosticHub` (WebSocket) to `http://localhost:5218` (the ASP.NET Core backend).

## Architecture

```
Pages (HomePage, DebugPage)          <- Route-level composition
Components (layout/, home/, debug/)  <- Presentational, read from stores
Stores (Zustand)                     <- Reactive state + async actions
Services (apiService, signalR)       <- Plain TS classes, no React
Types / Config                       <- Shared contracts
```

- **Services are plain TypeScript** — no React dependency, usable directly in Zustand store actions.
- **Zustand holds ALL async state** — Components call store actions and read via selectors.
- **Relative URLs** — the Vite dev proxy handles forwarding; in production, everything is same-origin.

## Features

### Home Page (`/`)
- List .NET processes running on the server
- Attach/detach diagnostic triggers
- Take memory snapshots and create dumps
- Upload crash dump files (.dmp/.mdmp) or open from server path

### Debug Page (`/debug/:sessionId`)
- **Real-time event monitoring** (CPU, GC allocations, exceptions, HTTP req/s, working set)
- **Query picker** with all 10 diagnostic queries from the WPF app
- **Master-Detail DataGrid** with column definitions, filtering, and path-based value extraction
- **GC Root Path analysis** with progress tracking and monospace-formatted paths
- **Custom Hex Viewer** (dark theme, 16-byte rows, incremental loading — zero npm deps)
- **Session management** — close sessions, expiration detection

### Toast Notifications
All API errors and key actions emit MUI Snackbar toasts with severity levels (success, info, warning, error).

## Project Structure

```
src/
  main.tsx                      <- Entry point
  App.tsx                       <- Root: theme, router, routes
  theme.ts                      <- MUI theme
  config/                       <- API URLs, gridRegistry
  types/                        <- TypeScript types (api.ts, signalr.ts)
  services/                     <- apiService.ts (fetch wrapper), signalRService.ts
  stores/                       <- Zustand stores
  components/
    layout/                     <- AppLayout, Header, Footer, ToastProvider
    home/                       <- ProcessPicker, SessionActions, DumpUploadDialog
    debug/                      <- EventsBar, QueryPicker, FilterBar, MasterDetailGrid, GcRootPanel, HexViewerDialog
    shared/                     <- HexViewer, HexViewer.module.css
  pages/                        <- HomePage, DebugPage
```

## Building for Production

```bash
npm run build
```

Output goes to `../DiagExperimentsSolution/DiagnosticServer/wwwroot/assets/`. The ASP.NET Core server serves the production build via `UseDefaultFiles()` + `UseStaticFiles()`.

## Backend Contract

### REST API
| Method | Endpoint | Purpose |
|---|---|---|
| GET | `/api/processes` | List .NET processes |
| POST | `/api/processes/attach/{id}` | Attach event triggers |
| POST | `/api/processes/detach` | Detach event triggers |
| POST | `/api/processes/snapshot/{id}` | Take memory snapshot |
| POST | `/api/processes/dump/{id}` | Create memory dump |
| GET | `/api/sessions` | List active sessions |
| GET | `/api/sessions/queries/metadata` | Query metadata (columns) |
| POST | `/api/sessions/open-dump` | Upload dump file (multipart) |
| POST | `/api/sessions/open-dump-path` | Open dump from server path |
| POST | `/api/sessions/{id}/{query}` | Execute query |
| POST | `/api/sessions/{id}/gcroot/{addr}` | GC root path analysis |
| POST | `/api/sessions/{id}/hex/{addr}` | Raw object bytes (base64) |
| DELETE | `/api/sessions/{id}` | Close session |

### SignalR Hub (`/diagnosticHub`)
- **Server->Client**: `onEvs`, `onMessage`, `onAlert`, `onGcRootProgress`, `onGcRootComplete`, `onSessionCreated`, `onSessionClosed`

## License

MIT — see LICENSE file at root of repository
