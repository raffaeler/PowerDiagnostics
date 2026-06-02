# DiagnosticWPF — Complete Functionality Reference

> **Purpose**: This document catalogs every feature, behavior, and data flow in the WPF desktop application, to serve as the **definitive specification** for re-implementing all capabilities in the `DiagnosticServer` ASP.NET Core backend and the `uidiag` React frontend.

---

## 1. Application Overview

**DiagnosticWPF** is a .NET 10 Windows desktop application that demonstrates automated production diagnostics for .NET applications. It is a **direct consumer** of the `ClrDiagnostics` library — no server intermediary. The app runs entirely in-process.

### 1.1 Architecture (WPF-specific)

```
DiagnosticWPF (.NET 10, WPF)
├── MainWindow           — Master-detail diagnostics view
│   ├── Trigger Bar      — Real-time event metrics (CPU, Memory, HTTP, Exceptions)
│   ├── Toolbar          — Open dump, Monitor process, Snapshot, Close, Query picker, Filter
│   ├── Master DataGrid  — Query results (programmatic columns)
│   ├── Detail DataGrid  — Sub-results (e.g., stack frames for a thread)
│   ├── Root Path Text   — GC root path trace for selected ClrObject
│   └── Status Bar       — Temporary status messages (auto-clear after 3.5s)
│
├── ProcessPicker        — Modal dialog: list .NET processes, select one
├── HexViewer            — Modal window: raw byte-level hex view of a ClrObject
│
├── Models/
│   ├── KnownGrids       — Static registry of Type → UIGrid (column definitions)
│   ├── UIGrid           — Describes a DataGrid: type, columns, details property
│   └── UIGridColumn     — Single column definition (header, path, format, alignment)
│
├── Helpers/
│   ├── DynamicGridMaker — Builds DataGridTemplateColumn from UIGridColumn at runtime
│   ├── FileHelper       — OpenFileDialog wrapper for .dmp files
│   └── ICollectionExtensions — AddRange<T> extension (note: has a bug, loops over `list` instead of `items`)
│
└── Resources/
    ├── Icons.xaml        — XAML vector icons (fileOpen, monitor, snapshot, close, etc.)
    └── ItemTemplates.xaml — DataTemplates (if any)
```

### 1.2 NuGet Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| `Microsoft.Diagnostics.Runtime` (ClrMD) | 4.0.726401 | Managed heap inspection, dump loading, GC root tracing |
| `Microsoft.Diagnostics.NETCore.Client` | 0.2.661903 | IPC pipe to .NET runtime (dump collection, EventPipe) |
| `Microsoft.Diagnostics.Tracing.TraceEvent` | 3.2.3 | Cross-platform ETW/EventPipe event tracing |
| `Microsoft.Diagnostics.Runtime.Utilities` | 4.0.726401 | ClrMD utilities |
| `WPFHexaEditor` | 2.1.7 | Hex editor control for raw memory viewing |

### 1.3 Project References

| Project | Purpose |
|---------|---------|
| `ClrDiagnostics` | Core diagnostic engine (DiagnosticAnalyzer, Triggers, Extensions) |
| `DiagnosticModels` | Shared data transfer objects (Dbm*, Evs*) |
| `DiagnosticInvestigations` | Query catalog (KnownQuery, QueriesService) |
| `CustomEventSource` | Custom ETW EventSource (`CustomHeaderEventSource`) |

---

## 2. MainWindow Layout

### 2.1 Full Visual Hierarchy

```
MainWindow (1200×600, "Diagnostic Demo by @raffaeler")
└── Grid (Margin=10)
    ├── Row 0: Trigger Headers Bar (StackPanel > Grid 2×11)
    │   ├── CPU [clickable header → clears trCpu]
    │   │   └── trCpu TextBlock (e.g., "45.2%")
    │   ├── Last GC Alloc [clickable header → clears trGcAlloc]
    │   │   └── trGcAlloc TextBlock (e.g., "8192")
    │   ├── Working-set [clickable header → clears trWorkingSet]
    │   │   └── trWorkingSet TextBlock (e.g., "256 MB")
    │   ├── Http Req/s [clickable header → clears trHttpReq]
    │   │   └── trHttpReq TextBlock (e.g., "42/sec")
    │   ├── Custom Header [clickable header → clears trCustomHeader]
    │   │   └── trCustomHeader TextBlock (e.g., "15")
    │   └── Last first-chance exception [clickable header → clears trException]
    │       └── trException TextBlock (e.g., "System.NullReferenceException: Object reference...")
    │
    ├── Row 1: Toolbar (StackPanel > horizontal)
    │   ├── Open Dump button (icon: fileOpen, tooltip: "Open a dump file")
    │   ├── Monitor Process button (icon: monitor, tooltip: "Monitor a process")
    │   ├── Snapshot button (icon: snapshot, tooltip: "Snapshot the process")
    │   ├── Close button (icon: close, tooltip: "Close the current session")
    │   ├── Query ComboBox (ComboQueries, minWidth=350, font=22)
    │   │   └── Cue text "Pick a query ..." overlays when no selection
    │   ├── Filter clear button (shows "Filter: ")
    │   └── Filter TextBox (FilterTextBlock, KeyUp→FilterChanged)
    │
    ├── Row 2: Master-Detail Area (Grid 3 rows)
    │   ├── Top row (3*): Master+Details (Grid 2 cols)
    │   │   ├── Master DataGrid (3*) — query results
    │   │   ├── GridSplitter (5px vertical)
    │   │   └── Details DataGrid (collapsed when no details)
    │   ├── GridSplitter (5px horizontal)
    │   └── Bottom row: textDetails TextBox (scrollable, monospace, read-only)
    │       — Shows GC root path trace; collapsed when empty
    │
    └── Row 3: Status Bar
        └── status TextBlock (font=16, gray, auto-clears)
```

### 2.2 Styling

| Style Key | Target | Key Properties |
|-----------|--------|---------------|
| `clearButtonStyle` | Button | Padding=5, Margin=15/5/0/5, FontSize=22, white bg, no border |
| `triggerHeader` | TextBlock | Margin=0/5/10/5, MinWidth=60, FontSize=14 |
| `triggerHeaderButton` | Button | Same as triggerHeader + white bg, no border, tooltip="Clear" |
| `triggerValue` | TextBlock | Margin=0/5/10/5, MinWidth=60, MaxWidth=600, FontSize=14, Bold, Wrap |
| `toolButtonStyle` | Button | 48×48, Margin=0/5/10/5, Padding=2, Stretch content |
| `smallButtonStyle` | Button | 26×26, Padding=1, Stretch content |
| `GridRowStyle` | DataGridRow | DoubleClick → GridDoubleClick event |

---

## 3. Complete Workflow / State Machine

### 3.1 Application Startup

1. `MainWindow()` constructor → `InitializeComponent()`
2. `Window_Loaded` event:
   - `InitializeQueries()` → creates `QueriesService`, stores `_queries` as `List<KnownQuery>`
   - Sets `ComboQueries.ItemsSource = _queries`
   - Disables `ComboQueries` (no data source yet)

### 3.2 Opening a Dump File

**Trigger**: Click "Open Dump" button

1. `Close(null, null)` — disposes any existing analyzer and resets UI
2. `FileHelper.OpenDialog(this, "Select a dump file")` — shows `OpenFileDialog`
   - Filters: `*.dmp` or all files
   - Single file selection
3. If file selected: `DiagnosticAnalyzer.FromDump(fi.FullName, true)`
   - `true` = `CacheAllObjects` (lazy cache of all heap objects)
4. On success: `ComboQueries.IsEnabled = true` → queries can be selected
5. On failure: shows error message in status bar
6. `UpdateStatus($"File {fi.Name} loaded")` — temporary status (3.5s auto-clear)

**DataTarget creation**: `DataTarget.LoadDump(filename)` → creates `ClrRuntime` from the first ClrVersion

### 3.3 Monitoring a Process (Live Event Tracing)

**Trigger**: Click "Monitor Process" button

1. `Close(null, null)` — disposes existing analyzer
2. Opens `ProcessPicker` as modal dialog
3. If user selects a process and clicks OK:
   - `_process = picker.SelectedProcess`
   - `SubscribeTriggers()`

#### 3.3.1 ProcessPicker Dialog

- Lists all running .NET processes (via `ProcessHelper.Default.GetDotnetProcesses()`)
- Columns: PID, Name
- Buttons: Refresh, OK, Cancel
- Returns `SelectedProcess` on OK

#### 3.3.2 SubscribeTriggers — EventPipe Subscription

1. `UnsubscribeTriggers()` — disposes previous `TriggerAll`
2. Creates `TriggerAll(processId, "Raf-CustomHeader", "TriggerHeader")`
3. Sets 6 callback actions:
   - `OnCpu` → `UpdateTextBlock(trCpu, d.ToString() + "%")`
   - `OnEventCounterCount` → `UpdateTextBlock(trCustomHeader, d.ToString())`
   - `OnException` → `UpdateTextBlock(trException, d)` (exception type + message string)
   - `OnGcAllocation` → `UpdateTextBlock(trGcAlloc, $"{d}")`
   - `OnHttpRequests` → `UpdateTextBlock(trHttpReq, $"{d}/sec")`
   - `OnWorkingSet` → `UpdateTextBlock(trWorkingSet, $"{d} MB")`
4. `_triggerAll.Start()`

**TriggerAll subscribes to these EventPipe providers:**

| Provider | Level | Keywords | Args | Data Retrieved |
|----------|-------|----------|------|---------------|
| `System.Runtime` | Informational | None | `EventCounterIntervalSec=1` | "cpu-usage" (Mean), "working-set" (Mean) |
| `Raf-CustomHeader` | Verbose | -1 | `EventCounterIntervalSec=1` | "TriggerHeader" (Count) |
| `Microsoft-Windows-DotNETRuntime` | Verbose | -1 | — | GCAllocationTick (AllocationAmount), ExceptionStart |
| `Microsoft-DotNETCore-SampleProfiler` | Verbose | All | — | Sampling profiler events |
| `Microsoft-AspNetCore-Hosting` | Informational | 0 | `EventCounterIntervalSec=1` | "requests-per-second" (Increment) |

**Event dispatching** (inside `TriggerAll`):
- `OnSubscribe`: Direct CLR parser subscriptions for `GCAllocationTick` and `ExceptionStart`
- `Dynamic.All` → `OnEvent`: Dispatches EventCounter payloads by `Name` field

#### 3.3.3 UpdateTextBlock — Thread-Safe UI Update

```csharp
Dispatcher.Invoke(() => { tb.Text = data; });
```
Exceptions from dispatcher are silently caught (for shutdown race conditions).

#### 3.3.4 ClearHeader — Clear Trigger Value

Each trigger header is a `Button` whose `Tag` binds to the corresponding `TextBlock`. Clicking it clears the text.

### 3.4 Taking a Snapshot

**Trigger**: Click "Snapshot" button

1. Guard: `_process` must be non-null (must have called Monitor Process first)
2. `ResetUI()` — clears grids and selection
3. `DiagnosticAnalyzer.FromSnapshot(_process.Id)`
   - Uses `DataTarget.CreateSnapshotAndAttach(pid)` — fast in-memory snapshot
   - Does **not** persist to disk
4. Shows elapsed time: `"Process {pid} snapshot took {ms}ms"`
5. Enables `ComboQueries`

### 3.5 Running Queries

#### 3.5.1 Query Selection

**Trigger**: `ComboQueries_SelectionChanged`

1. Gets `KnownQuery` from selected item
2. Hides `ComboCue` overlay text
3. Looks up `UIGrid` from `KnownGrids` by the query's `Type`
4. If the `UIGrid` has a `DetailsProperty`, also looks up the details `UIGrid` by `DetailsType`
5. `MakeGrid(uIGrid, detailsUiGrid)` — dynamically creates DataGrid columns
6. Sets `_currentQuery = item`
7. `item.Populate(_analyzer)` — executes the query, returns `IEnumerable`
8. Wraps result in `ListCollectionView` with `MasterFilter`
9. `Master.ItemsSource = _masterView`

#### 3.5.2 Dynamic Column Generation

`DynamicGridMaker.CreateGridColumn(UIGridColumn)`:
- Creates `DataGridTemplateColumn` with a `StackPanel` + `TextBlock` cell template
- Applies `Binding` from the column's `Path` with optional `StringFormat`
- Sets tooltip (static or bound via `TooltipPath`)
- Right-aligns numeric columns
- Uses `Courier New` font, size 16

#### 3.5.3 Filtering

**Trigger**: Typing in `FilterTextBlock` (KeyUp) or clicking "Filter: " clear button

1. `FilterChanged` → sets `_masterView.Filter = MasterFilter`
2. `MasterFilter` → delegates to `_currentQuery.Filter(obj, filterText)`
3. Filter returns `bool?` — `null`/`false` means exclude, `true` means include
4. Most filters do case-insensitive substring matching via `FilterBy` extension

### 3.6 Master-Detail Interaction

#### 3.6.1 Master Grid Selection

**Trigger**: `Master_SelectionChanged`

1. If selected item is `ClrObject`:
   - Calls `UpdateDetailsText(clrObject)` → GC root path tracing
2. If selected item has a `DetailsProperty`:
   - Extracts the property value (must be `IEnumerable`)
   - Sets `Details.ItemsSource = dataDetails`
3. Otherwise: `UpdateDetailsText(null)` → hides root path panel

#### 3.6.2 Detail Grid Selection

**Trigger**: `Details_SelectionChanged`

Same behavior as master selection for `ClrObject` → GC root path tracing.

#### 3.6.3 GC Root Path Tracing

**Trigger**: Selecting a `ClrObject` in either grid

`UpdateDetailsText(ClrObject?)`:
1. Cancels any previous root computation (`_updateDetailsTextCts.Cancel()`)
2. Clears `textDetails`
3. If `clrObject == null`: collapses the bottom details row
4. Gets path count via `_analyzer.GetGraphPathsCount(clrObject)`
5. If > 75 paths: shows confirmation dialog
6. Calls `_analyzer.PrintRootsAsync(clrObject, onProgress, token)`:
   - Shows progress % in status bar
   - Result is a multi-line text showing all root paths:
     ```
     TypeName Addr:0x... MT:0x... Size:...
     
     Root StaticVar Addr:0x...
       Path 0
          ObjectAddress1 TypeName1
               Objects whose fields point to ObjectAddress1
                    ReferencingObjAddress TypeName [static] field:FieldName
          ObjectAddress2 TypeName2
               Objects whose fields point to ObjectAddress2
                    ...
       Path 1
          ...
     ```
   - `FindReferencing` is called for each node in the path to show which objects hold references to it (from both instance and static fields)
7. On completion: expands details row to 3* height (min 100px)
8. Shows "Graph built successfully (N references)" status
9. On cancel: swallows `OperationCanceledException`
10. On error: shows error message in status

### 3.7 Hex Viewer (Raw Memory)

**Trigger**: Double-click a row in any DataGrid where the item is `ClrObject` or `ClrRoot`

`GridDoubleClick`:
1. Extracts `ClrObject` from the row item (for `ClrRoot`, uses `root.Object`)
2. `_analyzer.ReadRawContent(clrObject)`:
   - For `byte[]` arrays: reads via `AsArray().ReadValues<byte>()`
   - For other objects: reads `object.Size` bytes from `DataTarget.DataReader`
3. Opens `HexViewer` window with the byte array, using the `WpfHexaEditor` control
4. HexViewer is read-only, uses `ByteSpacerPositioning="Both"`, font size 16

### 3.8 Closing a Session

**Trigger**: Click "Close" button (or implicitly called before OpenDump/MonitorProcess)

1. `ResetUI()` — clears all grid columns and selections
2. `_analyzer.Dispose()` → `Close()` which disposes `ClrRuntime` and `DataTarget`
3. `_analyzer = null`
4. Disables `ComboQueries`
5. Clears status

---

## 4. All Diagnostic Queries (10 total)

These are defined in `QueriesService.CreateQueries()` and map to `DiagnosticAnalyzer` methods.

### 4.1 DumpHeapStat

| Property | Value |
|----------|-------|
| **Name** | `"DumpHeapStat"` |
| **Master Type** | `DbmDumpHeapStat` |
| **Details Type** | `ClrObject` (via `Objects` property) |
| **Analyzer Call** | `a.DumpHeapStat(0)` — SOS `dumpheap -stat` |
| **Data** | Groups objects by `ClrType`, returns type, count, total size |
| **Master Columns** | Type (Name), MT (MethodTable hex), Graph Size (total bytes) |
| **Filter** | Type name substring match |
| **Detail Columns** | Address (hex), Size, Type |

### 4.2 GetStaticFieldsWithGraphAndSize

| Property | Value |
|----------|-------|
| **Name** | `"GetStaticFieldsWithGraphAndSize"` |
| **Master Type** | `DbmStaticFields` |
| **Details Type** | `ClrObject` (via `Obj` → children graph nodes) |
| **Analyzer Call** | `a.GetStaticFieldsWithGraphAndSize()` |
| **Data** | All static fields that are object references, with full graph size and child graph |
| **Master Columns** | Static field name, Size, Object (type name) |
| **Filter** | Object type name substring match |
| **Detail Columns** | Address (hex), Size, Type |

### 4.3 GetDuplicateStrings

| Property | Value |
|----------|-------|
| **Name** | `"GetDuplicateStrings"` |
| **Master Type** | `DbmDupStrings` |
| **Details Type** | None |
| **Analyzer Call** | `a.GetDuplicateStrings()` (min count = 2) |
| **Data** | Dictionary of string → occurrence count |
| **Master Columns** | String (text), Count |
| **Filter** | String text substring match |

### 4.4 GetStringsBySize

| Property | Value |
|----------|-------|
| **Name** | `"GetStringsBySize"` |
| **Master Type** | `DbmStringsBySize` |
| **Details Type** | None |
| **Analyzer Call** | `a.GetStringsBySize(0)` (all sizes) |
| **Data** | Strings ordered by length descending |
| **Master Columns** | Object (type name), String (text), Size (length) |
| **Filter** | String text substring match |

### 4.5 Modules

| Property | Value |
|----------|-------|
| **Name** | `"Modules"` |
| **Master Type** | `ClrModule` |
| **Details Type** | None |
| **Analyzer Call** | `a.Modules.ToList()` |
| **Data** | All loaded CLR modules |
| **Master Columns** | AssemblyName, Name, Address (hex), Size |
| **Filter** | Module name substring match |

### 4.6 Threads Stacks

| Property | Value |
|----------|-------|
| **Name** | `"Threads stacks"` |
| **Master Type** | `DbmStackFrame` |
| **Details Type** | `ClrStackFrame` (via `StackFrames` property) |
| **Analyzer Call** | `a.Stacks()` |
| **Data** | Each thread with its stack frames |
| **Master Columns** | IsAlive, ManagedThreadId, Address (hex) |
| **Filter** | Thread address hex substring match |
| **Detail Columns** | FrameName, Method, Kind, StackPointer (hex) |

### 4.7 Roots

| Property | Value |
|----------|-------|
| **Name** | `"Roots"` |
| **Master Type** | `ClrRoot` |
| **Details Type** | None |
| **Analyzer Call** | `a.Roots.ToList()` |
| **Data** | All GC roots |
| **Master Columns** | Address (hex), Object (type), IsPinned |
| **Filter** | Root object type name substring match |

### 4.8 ObjectsBySize

| Property | Value |
|----------|-------|
| **Name** | `"ObjectsBySize"` |
| **Master Type** | `ClrObject` |
| **Details Type** | None |
| **Analyzer Call** | `a.GetObjectsBySize(1)` |
| **Data** | All objects > 1 byte, ordered by size descending |
| **Master Columns** | Address (hex), Size, Type |
| **Filter** | Type name substring match |

### 4.9 NonSystemObjectsBySize

| Property | Value |
|----------|-------|
| **Name** | `"NonSystemObjectsBySize"` |
| **Master Type** | `ClrObject` |
| **Details Type** | None |
| **Analyzer Call** | `a.GetObjectsBySize(1)` + filter |
| **Data** | Same as ObjectsBySize but excludes types starting with: `System`, `Microsoft`, `Interop`, `Internal`, plus free blocks |
| **Master Columns** | Address (hex), Size, Type |
| **Filter** | Type name substring match |

### 4.10 GetObjectsGroupedByAllocator

| Property | Value |
|----------|-------|
| **Name** | `"GetObjectsGroupedByAllocator (.NET5+ dumps)"` |
| **Master Type** | `DbmAllocatorGroup` |
| **Details Type** | `ClrObject` (via `Objects` property) |
| **Analyzer Call** | `a.GetObjectsGroupedByAllocator(a.Objects)` |
| **Data** | Objects grouped by AssemblyLoadContext/allocator. Uses `ClrType.AssemblyLoadContextAddress` (SOS8 interface, .NET 5+) |
| **Master Columns** | Allocator Address (hex), Allocator Size, Allocator Type, Allocator Name |
| **Filter** | Allocator name substring match |
| **Detail Columns** | Address (hex), Size, Type |

---

## 5. KnownGrids Registry — Complete Column Definitions

### 5.1 DbmDumpHeapStat → ClrObject

| Column Header | Binding Path | Format | Tooltip | Right-Aligned |
|--------------|-------------|--------|---------|:---:|
| Type | `Type` | — | `Type` | — |
| MT | `Type.MethodTable` | `0:X16` | `MethodTable` | — |
| Graph Size | `GraphSize` | `0:N0` | `GraphSize` | Yes |

**Details (ClrObject):**

| Column Header | Binding Path | Format | Tooltip | Right-Aligned |
|--------------|-------------|--------|---------|:---:|
| Address | `Address` | `0:X16` | `Address` | Yes |
| Size | `Size` | `0:N0` | `Size` | Yes |
| Type | `Type` | — | `Type` | — |

### 5.2 ClrObject (standalone)

Same as DbmDumpHeapStat details above.

### 5.3 ClrType

| Column Header | Binding Path | Format | Tooltip | Right-Aligned |
|--------------|-------------|--------|---------|:---:|
| Name | `Name` | — | `Name` | — |
| IsFree | `IsFree` | — | `IsFree` | — |
| Module Name | `Module.Name` | — | `Module.Name` | — |

### 5.4 DbmStaticFields → ClrObject

| Column Header | Binding Path | Format | Tooltip | Right-Aligned |
|--------------|-------------|--------|---------|:---:|
| Static field name | `Field.Name` | — | `Field.Name` | — |
| Size | `Size` | `0:N0` | `Size` | Yes |
| Object | `Obj` | — | `Obj` | — |

### 5.5 DbmDupStrings

| Column Header | Binding Path | Format | Tooltip | Right-Aligned |
|--------------|-------------|--------|---------|:---:|
| String | `Text` | — | `Text` | — |
| Count | `Count` | `0:N0` | `Count` | Yes |

### 5.6 DbmStringsBySize

| Column Header | Binding Path | Format | Tooltip | Right-Aligned |
|--------------|-------------|--------|---------|:---:|
| Object | `Obj` | — | `Obj` | — |
| String | `Text` | — | `Text` | — |
| Size | `Size` | `0:N0` | `Size` | Yes |

### 5.7 ClrModule

| Column Header | Binding Path | Format | Tooltip | Right-Aligned |
|--------------|-------------|--------|---------|:---:|
| AssemblyName | `AssemblyName` | — | `AssemblyName` | — |
| Name | `Name` | — | `Name` | — |
| Address | `Address` | `0:X16` | `Address` | Yes |
| Size | `Size` | `0:N0` | `Size` | Yes |

### 5.8 ClrThread

| Column Header | Binding Path | Format | Tooltip | Right-Aligned |
|--------------|-------------|--------|---------|:---:|
| IsAlive | `IsAlive` | — | `IsAlive` | — |
| ManagedThreadId | `ManagedThreadId` | — | `ManagedThreadId` | — |
| Address | `Address` | `0:X16` | `Address` | Yes |

### 5.9 DbmStackFrame → ClrStackFrame

**Master (DbmStackFrame):**

| Column Header | Binding Path | Format | Tooltip | Right-Aligned |
|--------------|-------------|--------|---------|:---:|
| IsAlive | `Thread.IsAlive` | — | `Thread.IsAlive` | — |
| ManagedThreadId | `Thread.ManagedThreadId` | — | `Thread.ManagedThreadId` | — |
| Address | `Thread.Address` | `0:X16` | `Thread.Address` | Yes |

**Details (ClrStackFrame):**

| Column Header | Binding Path | Format | Tooltip | Right-Aligned |
|--------------|-------------|--------|---------|:---:|
| FrameName | `FrameName` | — | `FrameName` | — |
| Method | `Method` | — | `Method` | — |
| Kind | `Kind` | — | `Kind` | — |
| StackPointer | `StackPointer` | `0:X16` | `StackPointer` | Yes |

### 5.10 ClrRoot

| Column Header | Binding Path | Format | Tooltip | Right-Aligned |
|--------------|-------------|--------|---------|:---:|
| Address | `Address` | `0:X16` | `Address` | Yes |
| Object | `Object` | — | `Object` | — |
| IsPinned | `IsPinned` | — | `IsPinned` | — |

### 5.11 DbmAllocatorGroup → ClrObject

| Column Header | Binding Path | Format | Tooltip | Right-Aligned |
|--------------|-------------|--------|---------|:---:|
| Allocator Address | `Allocator.Address` | `0:X16` | `Allocator.Address` | Yes |
| Allocator Size | `Allocator.Size` | `0:N0` | `Allocator.Size` | Yes |
| Allocator Type | `Allocator.Type` | — | `Allocator.Type` | — |
| Allocator Name | `Name` | — | `Name` | — |

---

## 6. DiagnosticAnalyzer API Surface (Used by WPF)

### 6.1 Factory Methods

| Method | Used? | Description |
|--------|:-----:|-------------|
| `FromDump(string filename, bool cacheObjects)` | Yes | Load a crash/heap dump file. `cacheObjects=true` caches `Objects` collection |
| `FromSnapshot(int pid)` | Yes | Fast in-memory snapshot via `DataTarget.CreateSnapshotAndAttach`. Not persisted to disk |
| `FromProcess(int pid)` | No (in WPF) | Attach to live process via `DataTarget.AttachToProcess` |
| `FromDump(int pid, ...)` | No | Create dump from process then load it |
| `FromSnapshot(string processName)` | No | Snapshot by process name |
| `FromProcess(string processName)` | No | Attach by process name |

### 6.2 Properties (Enumerables)

| Property | ClrMD Type | Description |
|----------|-----------|-------------|
| `MainAppDomain` | `ClrAppDomain` | First app domain |
| `Modules` | `IEnumerable<ClrModule>` | All loaded modules |
| `Handles` | `IEnumerable<ClrHandle>` | All GC handles |
| `Threads` | `IEnumerable<ClrThread>` | All managed threads |
| `Roots` | `IEnumerable<ClrRoot>` | All GC roots |
| `FinalizerRoots` | `IEnumerable<ClrRoot>` | Finalizer queue roots |
| `FinalizableObjects` | `IEnumerable<ClrObject>` | Objects awaiting finalization |
| `Objects` | `IEnumerable<ClrObject>` | All heap objects (cached if CacheAllObjects) |
| `ObjectsWithInstanceFields` | `IEnumerable<(ClrObject, ClrInstanceField, ulong)>` | Objects with instance field references |
| `ObjectsWithStaticFields` | `IEnumerable<(ClrObject, ClrStaticField, ulong)>` | Objects with static field references |

### 6.3 Analysis Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `DumpHeapStat(long minTotalSize)` | `IEnumerable<(ClrType?, List<ClrObject>, long)>` | SOS dumpheap -stat: group by type with count & total size |
| `GetStaticFields()` | `IEnumerable<(ClrStaticField, ClrObject)>` | All static fields that are object references |
| `GetStaticFieldsWithGraphSize()` | `IEnumerable<(ClrStaticField, ClrObject, ulong)>` | Same + graph size |
| `GetStaticFieldsWithGraphAndSize()` | `IEnumerable<(ClrStaticField, ClrObject, ulong, IEnumerable<ClrGraphNode>)>` | Same + full child graph |
| `GetDuplicateStrings(int minCount)` | `Dictionary<string, int>` | Duplicate string occurrences |
| `GetStringsBySize(long minSize, long maxSize)` | `IEnumerable<(ClrObject, string)>` | Strings ordered by length |
| `GetObjectsBySize(long minSize, bool excludeFreeBlocks)` | `IEnumerable<ClrObject>` | Objects > minSize, ordered desc |
| `GetObjectsGroupedByAllocator(IEnumerable<ClrObject>)` | `IEnumerable<(ClrObject, IEnumerable<ClrObject>)>` | Group by AssemblyLoadContext |
| `GetAllocatorName(ClrObject)` | `string` | Allocator name from `_name` field |
| `ObjectReferences(ClrObject)` | `IEnumerable<ClrObject>` | Child references |
| `ObjectReferencesWithFields(ClrObject)` | `IEnumerable<ClrReference>` | Child references with field info |
| `RootPaths(ClrObject)` | `IEnumerable<(ClrRoot, GCRoot.ChainLink)>` | GC root paths |
| `Stacks()` | `IEnumerable<(ClrThread, IEnumerable<ClrStackFrame>)>` | Thread stacks |
| `ReadRawContent(ClrObject)` | `byte[]` | Raw object bytes |
| `GetGraphPathsCount(ClrObject)` | `int` | Count of nodes in all root paths |
| `PrintRoots(ClrObject, Action<int>, CancellationToken)` | `string` | Formatted root path text |
| `PrintRootsAsync(ClrObject, Action<int>, CancellationToken)` | `Task<string>` | Async wrapper for PrintRoots |
| `GetObjectType(ulong)` | `ClrType?` | Resolve type by address |

### 6.4 Lifecycle

| Method | Description |
|--------|-------------|
| `Cancel()` | Cancels current operation, renews token |
| `Close()` | Disposes ClrRuntime and DataTarget |
| `Dispose()` | Standard IDisposable + GC.SuppressFinalize |

### 6.5 Properties (other)

| Property | Type | Description |
|----------|------|-------------|
| `CacheAllObjects` | `bool` | Set at construction; controls lazy caching of `Objects` |
| `Token` | `CancellationToken` | Current cancellation token |
| `DataReader` | `IDataReader` | Low-level memory reader |
| `OnGCRoot` | `Action<(long, CancellationToken)>?` | Progress callback for GC root operations |

---

## 7. Trigger System — Complete Reference

### 7.1 TriggerAll (Composite Trigger)

**Constructor**: `TriggerAll(int processId, string eventSourceName, string eventCounterName)`

In the WPF app, called with: `new TriggerAll(processId, "Raf-CustomHeader", "TriggerHeader")`

**Callback Properties:**

| Property | Type | Data Source | Display Format (WPF) |
|----------|------|-------------|----------------------|
| `OnCpu` | `Action<double>?` | `cpu-usage` EventCounter (Mean) | `"{d}%"` |
| `OnGcAllocation` | `Action<double>?` | `GCAllocationTick` (AllocationAmount) | `"{d}"` |
| `OnWorkingSet` | `Action<double>?` | `working-set` EventCounter (Mean) | `"{d} MB"` |
| `OnEventCounterCount` | `Action<double>?` | Custom EventCounter by name (Count) | `"{d}"` |
| `OnHttpRequests` | `Action<double>?` | `requests-per-second` EventCounter (Increment) | `"{d}/sec"` |
| `OnException` | `Action<string>?` | `ExceptionStart` CLR event | `"{ExceptionType}: {ExceptionMessage}"` |

**Lifecycle**: `Start()` → `Stop()` → `Dispose()` (standard IDisposable)

**Underlying TriggerBase class** (abstract):

| Method | Description |
|--------|-------------|
| `AddProvider(name, level, keywords, params)` | Add EventPipe provider |
| `AddKnownProvider(enum, level, keywords, params)` | Add by enum name |
| `Start(Action<TraceEvent>, Func<TraceEvent,bool>?)` | Start EventPipe session |
| `Stop()` | Stop session |
| `Dispose()` | Stop + cleanup |

### 7.2 Individual Trigger Classes

| Class | Purpose | Provider(s) |
|-------|---------|------------|
| `TriggerOnCpuLoad` | Fires when CPU > Threshold | System.Runtime |
| `TriggerOnExceptions` | Fires on CLR ExceptionStart | Microsoft-Windows-DotNETRuntime + SampleProfiler |
| `TriggerOnHttpRequests` | Fires on HTTP request counter events | Microsoft-AspNetCore-Hosting |
| `TriggerOnMemoryUsage` | Fires on GCAllocationTick + working-set | Microsoft-Windows-DotNETRuntime + System.Runtime |
| `TriggerOnEventCounter` | Fires on a specific EventCounter name | User-specified EventSource |

### 7.3 Known EventPipe Providers

| Enum Value | Provider String |
|------------|----------------|
| `Microsoft_Windows_DotNETRuntime` | `Microsoft-Windows-DotNETRuntime` |
| `System_Runtime` | `System.Runtime` |
| `Microsoft_DotNETCore_SampleProfiler` | `Microsoft-DotNETCore-SampleProfiler` |
| `Microsoft_AspNetCore_Hosting` | `Microsoft-AspNetCore-Hosting` |

---

## 8. Shared Data Models Reference

### 8.1 Dbm* (Heap Analysis Models)

| Model | Properties | Used By Query |
|-------|-----------|:---:|
| `DbmDumpHeapStat` | `ClrType? Type`, `List<ClrObject> Objects`, `long GraphSize` | DumpHeapStat |
| `DbmStaticFields` | `ClrStaticField? Field`, `ClrObject Obj`, `long Size` | StaticFields |
| `DbmDupStrings` | `string Text`, `int Count` | DuplicateStrings |
| `DbmStringsBySize` | `ClrObject Obj`, `string Text`, `long Size` (computed) | StringsBySize |
| `DbmAllocatorGroup` | `ClrObject Allocator`, `IEnumerable<ClrObject> Objects`, `string Name` | AllocatorGroups |
| `DbmStackFrame` | `ClrThread? Thread`, `IEnumerable<ClrStackFrame> StackFrames` | ThreadStacks |

### 8.2 Evs* (Event/Trace Models)

| Model | Base Class | Properties |
|-------|-----------|------------|
| `EvsCpu` | `EvsBaseDouble` | `Cat="CPU"`, `Uom="%"` |
| `EvsGcAllocation` | `EvsBaseDouble` | `Cat="Last GC Allocation"`, `Uom="bytes"` |
| `EvsWorkingSet` | `EvsBaseDouble` | `Cat="Working set"`, `Uom="MB"` |
| `EvsHttpRequests` | `EvsBaseDouble` | `Cat="HTTP Req/s"`, `Uom="/sec"` |
| `EvsException` | `EvsBaseString` | `Cat="Last first-chance Exception"` |
| `EvsCustomHeader` | `EvsBaseDouble` | `Cat="Custom header"` |

**Base classes**: `EvsBase` (abstract: `Cat`, `Val`, `Uom`), `EvsBaseDouble`, `EvsBaseString`

### 8.3 CustomEventSource

| Constant | Value | Description |
|----------|-------|-------------|
| `CustomHeaderEventSourceName` | `"Raf-CustomHeader"` | EventSource name |
| `TriggerHeaderName` | `"X-TriggerHeaderEventSource"` | HTTP header that triggers the counter |
| `TriggerHeaderCounterName` | `"TriggerHeader"` | EventCounter name |

`CustomHeaderEventSource` is an `EventSource` with:
- `RaiseTriggerHeaderCounter()` — increments counter (used by StressTestWebApp)
- `TriggerHeader` — `EventCounter` exposed to EventPipe

---

## 9. Key Data Flows

### 9.1 Dump Analysis Flow

```
User clicks "Open Dump"
  → FileHelper.OpenDialog (filter: *.dmp)
  → DiagnosticAnalyzer.FromDump(path, cacheObjects: true)
    → DataTarget.LoadDump(path)
    → Select first ClrVersion
    → CreateRuntime()
  → ComboQueries enabled
  → User picks query from ComboQueries
    → KnownGrids.TryGetUIGridByType(query.Type) → UIGrid
    → DynamicGridMaker.CreateGridColumn × N → DataGrid columns
    → query.Populate(_analyzer) → IEnumerable
    → ListCollectionView with filter → Master.ItemsSource
  → User types filter → MasterFilter → query.Filter(obj, text)
  → User clicks row → Master_SelectionChanged
    → If ClrObject → PrintRootsAsync (GC root path trace)
    → If has DetailsProperty → Details.ItemsSource = property value
  → User double-clicks row → GridDoubleClick
    → If ClrObject/ClrRoot → ReadRawContent → HexViewer
```

### 9.2 Live Monitoring Flow

```
User clicks "Monitor Process"
  → ProcessPicker dialog (ProcessHelper.GetDotnetProcesses)
  → User selects process
  → SubscribeTriggers()
    → TriggerAll(pid, "Raf-CustomHeader", "TriggerHeader")
    → Set 6 callbacks (CPU, GC Alloc, Working Set, HTTP, Custom, Exception)
    → TriggerAll.Start()
      → DiagnosticsClient.StartEventPipeSession(5 providers)
      → EventPipeEventSource.Process() (background task)
      → Events → Dynamic.All → OnEvent → callback → Dispatcher.Invoke → UI update
  → User clicks "Snapshot"
    → DiagnosticAnalyzer.FromSnapshot(pid)
      → DataTarget.CreateSnapshotAndAttach(pid)
    → ComboQueries enabled (same flow as dump analysis)
```

### 9.3 Close/Cleanup Flow

```
User clicks "Close"
  → ResetUI() — clears grids
  → _analyzer.Dispose()
    → ClrRuntime.Dispose()
    → DataTarget.Dispose()
  → UnsubscribeTriggers()
    → _triggerAll.Dispose()
      → Stop() — EventPipeSession.Dispose(), EventPipeEventSource.Dispose()
```

---

## 10. Checklist for Server + React Re-implementation

This section enumerates every behavior that must be reproduced in the `DiagnosticServer` + `uidiag` combination.

### 10.1 Session Management
- [ ] Create `DiagnosticAnalyzer` from dump file path
- [ ] Create `DiagnosticAnalyzer` from process snapshot
- [ ] Dispose analyzer and release resources
- [ ] Track session state (which process/dump is active)

### 10.2 Process Discovery
- [ ] List all running .NET processes (PID + Name)
- [ ] Client-side process picker UI

### 10.3 Query Execution
- [ ] Execute all 10 query types and return results
- [ ] Results must include the same data as the WPF queries (with Dbm* models or equivalent)
- [ ] Support server-side filtering

### 10.4 Grid Display
- [ ] 10 master grid configurations (same columns, formats, alignments)
- [ ] 3 detail grid configurations (ClrObject, ClrStackFrame, allocator Objects)
- [ ] All column formatting: hex addresses (`0:X16`), integers (`0:N0`), right-alignment

### 10.5 Live Event Monitoring (Triggers)
- [ ] Subscribe to EventPipe from target process (same 5 providers)
- [ ] Display real-time: CPU %, GC allocation, working set (MB), HTTP req/s, custom EventCounter, last exception
- [ ] Clear individual trigger values
- [ ] Start/stop monitoring

### 10.6 GC Root Path Tracing
- [ ] Select a `ClrObject` → show GC root paths
- [ ] Progress indication during long computations
- [ ] User confirmation for >75 references
- [ ] Cancellation support
- [ ] Formatted output with referencing fields (instance + static)

### 10.7 Hex Viewer
- [ ] Double-click object → raw bytes view
- [ ] Read-only hex display with byte spacing

### 10.8 Master-Detail Navigation
- [ ] Master grid selection → detail grid population (when DetailsProperty exists)
- [ ] Detail grid selection → GC root path for ClrObject
- [ ] Resizable master/detail split

### 10.9 Filtering
- [ ] Text filter on master grid
- [ ] Case-insensitive substring match on the appropriate property per query
- [ ] Clear filter button

### 10.10 Status / Feedback
- [ ] Temporary status messages that auto-clear
- [ ] Error display when operations fail
- [ ] Load time display for snapshots

---

## 11. Notes for Server Implementation

### 11.1 Serialization Considerations
`ClrMD` types (`ClrObject`, `ClrType`, `ClrThread`, `ClrRoot`, etc.) reference the `ClrRuntime` and are **not designed for serialization**. The server must:
- Either project them into serializable DTOs before sending over HTTP/SignalR
- Or use the existing `DiagnosticModels` (which already wrap ClrMD types in some cases, but not completely — `DbmDumpHeapStat` still holds `ClrType` and `List<ClrObject>`)

**Recommendation**: Create pure DTO versions of each model that contain only primitive/serializable types. For example:
```csharp
public class DumpHeapStatDto {
    public string? TypeName { get; set; }
    public ulong MethodTable { get; set; }
    public int ObjectCount { get; set; }
    public long TotalSize { get; set; }
    public List<ClrObjectSummaryDto> Objects { get; set; }
}
```

### 11.2 Trigger System for Server
The `TriggerAll` class uses `Task.Run` to process EventPipe on a background thread and fires callbacks. In the server:
- Map trigger callbacks to SignalR hub method invocations
- Each connected client should receive updates when the server monitors a process
- Use `IHubContext<DiagnosticHub>` to push events

### 11.3 GC Root Path Tracing
`PrintRootsAsync` returns a formatted string. The server can:
- Return the string as-is to the React client
- Or return a structured object (list of paths, each with list of nodes) and let the client format it

### 11.4 Hex Viewer Data
`ReadRawContent` returns `byte[]`. For the web client:
- Send as base64-encoded string
- Use a JavaScript hex viewer component

### 11.5 Snapshot vs Dump
- `FromSnapshot` is fast but ephemeral (no file on disk)
- `FromDump(int pid)` creates a temp dump file, then loads it — slower but can be persisted
- The API should expose both paths

---

*Document generated for cross-reference during DiagnosticServer + uidiag implementation.*
