---
name: 'UIDiag — React Frontend'
description: 'Coding conventions for the uidiag React+TypeScript+Vite frontend'
applyTo: 'uidiag/**'
---

# UIDiag — PowerDiagnostics React Frontend

## Overview
`uidiag` is the TypeScript-based React frontend for PowerDiagnostics, built with Vite, Material UI (MUI), Zustand, and SignalR. It replaces the legacy JavaScript `diagnostic-ui` with a more maintainable, type-safe architecture.

## Tech Stack
- **Runtime**: React 18+ with TypeScript
- **Bundler**: Vite
- **UI Library**: Material UI (MUI) v6+
- **State Management**: Zustand
- **Routing**: React Router v6
- **Real-time**: @microsoft/signalr
- **Visualization**: react-json-tree

## Architecture

### Layer Model
```
┌─────────────────────────────────────────┐
│  Pages (HomePage, DebugPage)            │  ← Route-level composition
├─────────────────────────────────────────┤
│  Components (layout/, home/, debug/)    │  ← Presentational, read from stores
├─────────────────────────────────────────┤
│  Stores (Zustand)                       │  ← Reactive state + async actions
├─────────────────────────────────────────┤
│  Services (apiService, signalR, auth)   │  ← Plain TS classes, no React
├─────────────────────────────────────────┤
│  Types / Config                         │  ← Shared contracts
└─────────────────────────────────────────┘
```

### Key Architectural Decisions
- **Services are plain TypeScript classes** — they don't depend on React. This allows them to be used directly in Zustand store actions.
- **Zustand stores hold ALL async state** — Components are purely presentational: they call store actions and read state via selectors. No `useEffect` + `useState` data fetching in components.
- **SignalR lifecycle** is managed by `SignalRService` (singleton). `useSignalRStore` bridges it to React.
- **API and SignalR use relative URLs** — the Vite dev proxy forwards `/api/*` and `/diagnosticHub` to the backend; in production (served from wwwroot) they are same-origin. Never construct absolute URLs with `getApiBaseUrl()`.
- **Auth is mock only** — username stored in `localStorage`, no real tokens or backend auth.

## Code Conventions

### Naming
| Construct | Convention | Example |
|-----------|-----------|---------|
| Components | `PascalCase` | `ProcessPicker`, `AppLayout` |
| Stores | `use` prefix + `Store` suffix | `useAppStore`, `useDiagnosticsStore` |
| Services | `camelCase` singleton | `apiService`, `signalRService`, `authService` |
| Types/Interfaces | `PascalCase` | `ProcessInfo`, `ApiResponse<T>` |
| Functions | `camelCase` | `getApiBaseUrl`, `fetchProcesses` |
| Constants | `UPPER_SNAKE_CASE` | `API_PROCESSES`, `HUB_PATH` |

### File Organization
```
src/
├── main.tsx            ← Entry point
├── App.tsx             ← Root: theme, router, routes
├── theme.ts            ← MUI theme customization
├── vite-env.d.ts       ← Vite type declarations
├── config/             ← Configuration: API URLs, env accessors
├── types/              ← TypeScript type definitions
│   ├── api.ts          ← API request/response types
│   ├── signalr.ts      ← SignalR event types
│   └── diagnostics.ts  ← Re-exports for convenience
├── services/           ← Plain TS service classes (no React)
│   ├── apiService.ts   ← Fetch wrapper for REST API (uses relative URLs)
│   ├── signalRService.ts ← SignalR HubConnection lifecycle (uses relative URL)
│   └── authService.ts  ← Mock auth (localStorage)
├── stores/             ← Zustand stores
│   ├── useAppStore.ts  ← User/auth state
│   ├── useDiagnosticsStore.ts ← Diagnostics domain state + actions
│   ├── useSignalRStore.ts ← SignalR connection + event buffer
│   └── useToastStore.ts ← Toast notification state (global, non-React)
├── components/
│   ├── layout/         ← AppLayout, Header, Footer, ToastProvider
│   ├── home/           ← ProcessPicker, SessionActions, DumpUploadDialog
│   ├── debug/          ← EventsBar, SessionList, QueryRunner, MasterDetailGrid, FilterBar, QueryPicker, HexViewerDialog, GcRootPanel
│   └── shared/         ← HexViewer, HexViewer.module.css, JsonTree
├── pages/              ← Route page compositions
│   ├── HomePage.tsx
│   └── DebugPage.tsx
└── hooks/              ← Shared React hooks (future use)
```

### Component Patterns
- **Prefer functional components** with hooks.
- **Extract complex markup** into separate components (decouple functionalities).
- **Use MUI `sx` prop** for one-off styles; use `theme.ts` for global overrides.
- **Components read from stores** via Zustand selectors — no prop drilling for global state.
- **Async operations** belong in store actions, not in `useEffect` callbacks.

### TypeScript
- **`strict: true`** is always enabled.
- **`noUnusedLocals` and `noUnusedParameters`** are enabled.
- **Avoid `any`** — use `unknown` and narrow with type guards.
- **Use `import type`** for type-only imports.

### Service / Network Layer (CRITICAL)
- **Always use relative URLs** for both REST API calls (`fetch('/api/...')`) and SignalR connections (`withUrl('/diagnosticHub')`).
- **Never construct absolute URLs** with `getApiBaseUrl()` — it bypasses the Vite proxy in development and causes CORS issues.
- The Vite dev proxy handles forwarding `/api/*` and `/diagnosticHub` (WebSocket) to the backend.
- In production (served from `wwwroot` by the ASP.NET backend), relative URLs are same-origin.

## Configuration

| Env Variable | Purpose | Default |
|---|---|---|
| `VITE_API_BASE_URL` | Backend base URL (deprecated — use relative URLs) | `http://localhost:5218` |

- `.env` — committed defaults.
- `.env.development` — local overrides.

## Building & Running

```bash
npm install
npm run dev       # Dev server on localhost:3000
npm run build     # Production build → ../DiagExperimentsSolution/DiagnosticServer/wwwroot/
```

The Vite dev server proxies:
- `/api/*` → `http://localhost:5218`
- `/diagnosticHub` → `http://localhost:5218` (WebSocket)

## Backend Contract

### REST API
| Method | Endpoint | Purpose |
|--------|----------|---------|
| GET | `/api/processes` | List .NET processes |
| POST | `/api/processes/attach/{id}` | Attach event triggers |
| POST | `/api/processes/detach` | Detach event triggers |
| POST | `/api/processes/snapshot/{id}` | Take memory snapshot |
| POST | `/api/processes/dump/{id}` | Create memory dump |
| GET | `/api/sessions` | List active sessions |
| GET | `/api/sessions/queries` | List available queries |
| GET | `/api/sessions/queries/metadata` | Query metadata with column definitions |
| POST | `/api/sessions/open-dump` | Upload dump file (multipart) |
| POST | `/api/sessions/open-dump-path` | Open dump from server path |
| POST | `/api/sessions/{sessionId}/{query}` | Execute query |
| POST | `/api/sessions/{sessionId}/gcroot/{addr}` | GC root path analysis |
| POST | `/api/sessions/{sessionId}/hex/{addr}` | Raw object bytes (base64) |
| DELETE | `/api/sessions/{sessionId}` | Close session |

### SignalR Hub (`/diagnosticHub`)
- **Server→Client**: `onEvs` (diagnostic event JSON), `onMessage`, `onAlert`, `onGcRootProgress`, `onGcRootComplete`, `onSessionCreated`, `onSessionClosed`
- **Client→Server**: `SendMessage(user, message)`

## Extension Points
- New pages: add route in `App.tsx`, create page component in `pages/`, add nav button in `Header.tsx`.
- New diagnostic features: extend `useDiagnosticsStore` with new actions, create dedicated component in `components/`.
- Real auth: Replace `authService.ts` and `useAppStore` login with token-based flow.
