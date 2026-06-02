# GCRoot Migration: Alternatives to `o => true`

## Background

In ClrMD v3, `GCRoot` was rewritten with a different usage pattern:

| | v2 | v3 |
|---|---|---|
| **Targets** | Passed per-call: `EnumerateGCRoots(targetAddress)` | Passed at construction: `new GCRoot(heap, targets)` or `new GCRoot(heap, predicate)` |
| **Path type** | `GCRootPath` (with `Root` + `ImmutableArray<ClrObject> Path`) | `(ClrRoot Root, GCRoot.ChainLink Path)` tuple |
| **Progress** | `ProgressUpdated` event | Removed — no progress callbacks |
| **Single path** | `FindSinglePath(src, tgt)` | `FindPathFrom(startObj)` |

See [Migrating3.md](https://github.com/microsoft/clrmd/blob/main/doc/Migrating3.md) for the full migration guide.

## Problem

The current code uses `new GCRoot(heap, o => true)`, which matches **every object on the heap**. This means `EnumerateRootPaths()` walks the entire object graph for all objects — extremely slow and semantically wrong. In v2, the equivalent call was per-object: `EnumerateGCRoots(specificTargetAddress)`.

## Alternatives

### Option 1: Per-call GCRoot (simplest)

Drop the cached `_gcroot` entirely; create a fresh `GCRoot` per call with the specific target address.

```csharp
public IEnumerable<(ClrRoot Root, GCRoot.ChainLink Path)> RootPaths(ClrObject @object)
{
    var gcroot = new GCRoot(_clrRuntime.Heap, new[] { @object.Address });
    return gcroot.EnumerateRootPaths();
}
```

| Pros | Cons |
|------|------|
| Exact v2 semantics | Reconstructs GCRoot on every call |
| Minimal code | Small allocation overhead |
| No caching complexity | |

**Best when:** `RootPaths` is called infrequently or on many different objects.

---

### Option 2: Cached GCRoot per target address

Cache one `GCRoot` per target, keyed by address. Reuses `GCRoot` for repeated calls on the same object.

```csharp
private Dictionary<ulong, GCRoot> _gcrootCache = new();

public IEnumerable<(ClrRoot Root, GCRoot.ChainLink Path)> RootPaths(ClrObject @object)
{
    if (!_gcrootCache.TryGetValue(@object.Address, out var gcroot))
    {
        gcroot = new GCRoot(_clrRuntime.Heap, new[] { @object.Address });
        _gcrootCache[@object.Address] = gcroot;
    }
    return gcroot.EnumerateRootPaths();
}
```

| Pros | Cons |
|------|------|
| Reuses GCRoot per target | Dictionary overhead |
| Good for repeated calls | GCRoot instances live until disposal |

**Best when:** `RootPaths` is called many times on the same objects (e.g., interactive UI).

---

### Option 3: Predicate with cached target **(selected)**

Use a single `GCRoot` instance, but construct it with a predicate that matches a specific stored target address. When the target changes, the `GCRoot` is reconstructed.

```csharp
private ulong _gcrootTarget;
private GCRoot? _gcroot;

private GCRoot GetOrCreateGCRoot(ulong targetAddress)
{
    if (_gcroot == null || _gcrootTarget != targetAddress)
    {
        _gcrootTarget = targetAddress;
        _gcroot = new GCRoot(_clrRuntime.Heap, o => o.Address == targetAddress);
    }
    return _gcroot;
}

public IEnumerable<(ClrRoot Root, GCRoot.ChainLink Path)> RootPaths(ClrObject @object)
{
    var gcroot = GetOrCreateGCRoot(@object.Address);
    return gcroot.EnumerateRootPaths();
}
```

| Pros | Cons |
|------|------|
| Single GCRoot instance | Thrashes on interleaved targets (A, B, A, B...) |
| Reuses for sequential calls on same target | |
| Matches typical usage (analyze one object at a time) | |

**Best when:** Objects are analyzed sequentially — the most common diagnostic workflow.

---

## Decision

**Option 3** was chosen for PowerDiagnostics because:
1. The typical workflow analyzes one object at a time, so sequential reuse is common
2. Single `GCRoot` instance simplifies disposal
3. The predicate-based approach (`o => o.Address == targetAddress`) achieves exact v2-equivalent filtering
