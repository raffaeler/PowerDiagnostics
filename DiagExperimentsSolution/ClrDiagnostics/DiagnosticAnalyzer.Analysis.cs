using ClrDiagnostics.Extensions;
using ClrDiagnostics.Models;

using Microsoft.Diagnostics.Runtime;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;

using static Microsoft.Diagnostics.Runtime.GCRoot;
//using Microsoft.Diagnostics.Runtime.Interfaces; // required by Microsoft.Diagnostics.Runtime version 3.0

namespace ClrDiagnostics;

public partial class DiagnosticAnalyzer
{
    public IDataReader DataReader => _dataTarget.DataReader;

    public ClrAppDomain MainAppDomain => _clrRuntime.AppDomains.First();
    public IEnumerable<ClrModule> Modules => _clrRuntime.EnumerateModules();
    public IEnumerable<ClrHandle> Handles => _clrRuntime.EnumerateHandles();
    public IEnumerable<ClrThread> Threads => _clrRuntime.Threads;

    // Heap
    public IEnumerable<ClrRoot> Roots => _clrRuntime.Heap.EnumerateRoots();
    public IEnumerable<ClrRoot> FinalizerRoots => _clrRuntime.Heap.EnumerateFinalizerRoots();
    public IEnumerable<ClrObject> FinalizableObjects => _clrRuntime.Heap.EnumerateFinalizableObjects();
    public IEnumerable<ClrObject> Objects
    {
        get
        {
            if (CacheAllObjects)
            {
                if (_cachedAllObjects == null)
                    _cachedAllObjects = _clrRuntime.Heap.EnumerateObjects().ToList();
                return _cachedAllObjects;
            }

            _cachedAllObjects = null;
            return _clrRuntime.Heap.EnumerateObjects();
        }
    }

    public IEnumerable<(ClrObject, ClrInstanceField, ulong)> ObjectsWithInstanceFields
    {
        get
        {
            if (CacheAllObjects)
            {
                if (_objectsWithInstanceFields == null)
                {
                    _objectsWithInstanceFields = Objects
                            .SelectMany(o => o.Type!.Fields,
                                    (o, f) => (obj: o, @field: f))
                            .Where(t => t.@field.IsObjectReference)
                            .Select(t => (@object: t.obj, @field: t.@field, address: t.@field.Read<ulong>(t.obj.Address, false)))
                            .ToList();
                }

                return _objectsWithInstanceFields;
            }

            return Objects
                .SelectMany(o => o.Type!.Fields, (o, f) => (obj: o, @field: f))
                .Where(t => t.@field.IsObjectReference)
                .Select(t => (@object: t.obj, @field: t.@field, address: t.@field.Read<ulong>(t.obj.Address, false)));
        }
    }

    public IEnumerable<(ClrObject, ClrStaticField, ulong)> ObjectsWithStaticFields
    {
        get
        {
            if (CacheAllObjects)
            {
                if (_objectsWithStaticFields == null)
                {
                    _objectsWithStaticFields = Objects
                        .SelectMany(o => o.Type!.StaticFields, (o, f) => (obj: o, @field: f))
                        .Where(t => t.@field.IsObjectReference)
                        .Select(t => (@object: t.obj, @field: t.@field, address: t.@field.Read<ulong>(MainAppDomain)))
                        .ToList();
                }

                return _objectsWithStaticFields;
            }

            return Objects
                .SelectMany(o => o.Type!.StaticFields, (o, f) => (obj: o, @field: f))
                .Where(t => t.@field.IsObjectReference)
                .Select(t => (@object: t.obj, @field: t.@field, address: t.@field.Read<ulong>(MainAppDomain)));

        }
    }

    // Walk down the graph starting from the given object
    public IEnumerable<ClrObject> ObjectReferences(ClrObject @object)
    {
        // required by Microsoft.Diagnostics.Runtime version 3.0
        return @object.EnumerateReferences(false, true);

        // v2:
        //return _clrRuntime.Heap.EnumerateObjectReferences(@object.Address, @object.Type, false, true);
    }

    public IEnumerable<ClrReference> ObjectReferencesWithFields(ClrObject @object)
    {
        // required by Microsoft.Diagnostics.Runtime version 3.0
        return @object.EnumerateReferencesWithFields(false, true);

        // v2
        //return _clrRuntime.Heap.EnumerateReferencesWithFields(@object.Address, @object.Type, false, true);
    }

    /// <summary>
    /// Given an object, returns the static field and the complete path that
    /// keeps it alive, starting from the static field reference and ending
    /// with the given object.
    /// </summary>
    /// <param name="clrObject">The object that we are trying to find paths for</param>
    /// <returns>The path for each static field, if any</returns>
    public IEnumerable<(ClrStaticField, ChainLink)> FindPathsToStatics(ClrObject clrObject,
        CancellationToken token = default)
    {
        var staticFields = GetStaticFields()
            .ToList();

        List<(ClrStaticField, ChainLink)> results = [];
        foreach (var root in staticFields)
        {
            token.ThrowIfCancellationRequested();

            GCRoot gcRoot = new(Heap, [clrObject.Address]);
            ChainLink? link = gcRoot.FindPathFrom(root.obj);
            if (link is null) continue;
            results.Add((root.field, link));
        }

        return results;
    }

    public IList<(ClrRoot? Root, ClrStaticField? StaticField, GCRoot.ChainLink Path)> GetRootPaths(ClrObject @object)
    {
        List<(ClrRoot? Root, ClrStaticField? StaticField, GCRoot.ChainLink Path)> paths = [];

        var address = @object.Address;
        GCRoot gcroot = new(_clrRuntime.Heap, (ClrObject o) => o.Address == address);

        paths = gcroot.EnumerateRootPaths(Token)
            .Select(x => (Root: (ClrRoot?)x.Root,
                          StaticField: (ClrStaticField?)null,
                          Path: x.Path))
            .ToList();

        if(ApplyNet10DatasStaticWorkaround)
        {
            // .NET 10+ static field fallback: when GCRoot returns empty, trace upward
            // through instance+static field references via FindReferencing.
            paths.AddRange(FindPathsToStatics(@object, Token)
                .Select(t => (Root: (ClrRoot?)null,
                              StaticField: (ClrStaticField?)t.Item1,
                              Path: t.Item2)));
        }

        return paths;
    }


    // EnumerateAllPaths — no direct replacement in v3
    //
    //public IEnumerable<LinkedList<ClrObject>> PathsBetween(ClrObject source, ClrObject target)
    //{
    //    return _gcroot.EnumerateAllPaths(source.Address, target.Address, false, Token);
    //}

    public IEnumerable<(ClrThread thread, IEnumerable<ClrStackFrame> stackFrames)> Stacks()
    {
        return _clrRuntime.Threads
            //.Where(t => t.IsAlive && !t.IsFinalizer && t.ManagedThreadId > 0)
            .Select(t => (t, t.EnumerateStackTrace()));
    }


    public IEnumerable<ClrObject> GetObjectsBySize(long minSize = 1024, bool excludeFreeBlocks = true)
    {
        return Objects
            .Where(o => o.Size > (ulong)minSize)
            .Where(o => !o.IsFree || !excludeFreeBlocks)
            .OrderByDescending(o => o.Size);
    }

    public byte[] ReadRawContent(ClrObject @object)
    {
        var address = @object.Address;
        var length = @object.Size;
        if (@object.IsArray && @object.Type!.Name == "Byte")
        {
            var arr = @object.AsArray();
            var bytes = arr.ReadValues<byte>(0, arr.Length);
            return bytes!;
        }


        byte[] blob = new byte[length];
        _dataTarget.DataReader.Read(address, blob);
        return blob;
    }

    // ──────────────────────── Memory Map ────────────────────────

    /// <summary>
    /// Returns information about all GC heap segments in the loaded dump,
    /// including their address ranges, kind, and object counts.
    /// </summary>
    public IEnumerable<DiagnosticModels.MemorySegmentInfo> GetMemorySegments()
    {
        var heap = _clrRuntime.Heap;
        foreach (var segment in heap.Segments)
        {
            long objectCount = 0;
            try
            {
                objectCount = segment.EnumerateObjectAddresses(heap, _ => true).Count();
            }
            catch
            {
                // Counting may fail on corrupted segments — report 0.
            }

            yield return new DiagnosticModels.MemorySegmentInfo
            {
                StartAddress = $"0x{segment.Start:X16}",
                EndAddress = $"0x{segment.End:X16}",
                CommittedStart = $"0x{segment.CommittedMemory.Start:X16}",
                CommittedEnd = $"0x{segment.CommittedMemory.End:X16}",
                ReservedStart = $"0x{segment.ReservedMemory.Start:X16}",
                ReservedEnd = $"0x{segment.ReservedMemory.End:X16}",
                SegmentKind = segment.Kind == GCSegmentKind.Large ? "Large" :
                              segment.Kind == GCSegmentKind.Pinned ? "Pinned" :
                              segment.Kind == GCSegmentKind.Frozen ? "Frozen" :
                              segment.Kind == GCSegmentKind.Ephemeral ? "Ephemeral" :
                              segment.Kind.ToString(),
                IsLargeObject = segment.Kind == GCSegmentKind.Large,
                IsPinnedObject = segment.Kind == GCSegmentKind.Pinned,
                ObjectCount = objectCount,
                Size = (long)(segment.End - segment.Start),
            };
        }
    }

    // ──────────────────────── Raw Memory Read ────────────────────────

    /// <summary>
    /// Reads raw bytes at an arbitrary address in the dump, not limited to
    /// ClrObject start addresses. Returns empty array if the range is
    /// outside all committed segments.
    /// </summary>
    /// <param name="address">The start address to read from.</param>
    /// <param name="length">Number of bytes to read (capped at 4096).</param>
    public byte[] ReadRawMemory(ulong address, int length)
    {
        if (length <= 0) return Array.Empty<byte>();
        if (length > 4096) length = 4096;

        // Bounds check: verify at least partially within a committed segment
        var heap = _clrRuntime.Heap;
        bool inBounds = false;
        foreach (var segment in heap.Segments)
        {
            if (address >= segment.CommittedMemory.Start &&
                address < segment.CommittedMemory.End)
            {
                inBounds = true;
                // Clamp length to not exceed segment committed end
                ulong maxReadable = segment.CommittedMemory.End - address;
                if ((ulong)length > maxReadable)
                    length = (int)maxReadable;
                break;
            }
        }

        if (!inBounds) return Array.Empty<byte>();

        byte[] buffer = new byte[length];
        try
        {
            int bytesRead = _dataTarget.DataReader.Read(address, buffer);
            if (bytesRead < length)
            {
                var trimmed = new byte[bytesRead];
                Array.Copy(buffer, trimmed, bytesRead);
                return trimmed;
            }
        }
        catch
        {
            // Address may be unmapped/unreadable — return whatever was read or empty.
            return Array.Empty<byte>();
        }

        return buffer;
    }

    // ──────────────────────── Object Field Layout ────────────────────────

    /// <summary>
    /// Returns the field layout of a heap object, with offset, type, and
    /// reference annotations for every field.
    /// </summary>
    /// <param name="address">The exact start address of a ClrObject.</param>
    /// <returns>Field layout or null if no object exists at this address.</returns>
    public DiagnosticModels.ObjectFieldLayout? GetObjectFieldLayout(ulong address)
    {
        var obj = FindObjectAtAddress(address);
        if (obj is not { } resolved || resolved.Type is null)
            return null;

        var fields = new List<DiagnosticModels.FieldInfo>();
        foreach (var field in resolved.Type.Fields.OrderBy(f => f.Offset))
        {
            if (field.Offset < 0) continue; // skip static fields (negative offset)

            string valueHex;
            string? targetAddressHex = null;

            if (field.IsObjectReference)
            {
                ulong refValue = field.Read<ulong>(address, interior: false);
                valueHex = $"0x{refValue:X16}";
                // Only mark as a valid target if it points to a known object
                targetAddressHex = FindObjectAtAddress(refValue) is not null
                    ? $"0x{refValue:X16}"
                    : null;
            }
            else
            {
                valueHex = field.ElementType switch
                {
                    ClrElementType.Boolean => field.Read<bool>(address, interior: false).ToString() ?? "false",
                    ClrElementType.Char => $"'{field.Read<char>(address, interior: false)}'",
                    ClrElementType.Int8 => field.Read<sbyte>(address, interior: false).ToString(),
                    ClrElementType.Int16 => field.Read<short>(address, interior: false).ToString(),
                    ClrElementType.Int32 => field.Read<int>(address, interior: false).ToString(),
                    ClrElementType.Int64 => field.Read<long>(address, interior: false).ToString(),
                    ClrElementType.UInt8 => field.Read<byte>(address, interior: false).ToString(),
                    ClrElementType.UInt16 => field.Read<ushort>(address, interior: false).ToString(),
                    ClrElementType.UInt32 => field.Read<uint>(address, interior: false).ToString(),
                    ClrElementType.UInt64 => field.Read<ulong>(address, interior: false).ToString("X16"),
                    ClrElementType.Float => field.Read<float>(address, interior: false).ToString("G"),
                    ClrElementType.Double => field.Read<double>(address, interior: false).ToString("G"),
                    _ => "<struct>",
                };
            }

            fields.Add(new DiagnosticModels.FieldInfo
            {
                Offset = field.Offset,
                FieldName = field.Name ?? "?",
                TypeName = field.Type?.Name ?? "?",
                IsObjectReference = field.IsObjectReference,
                ValueHex = valueHex,
                TargetAddressHex = targetAddressHex,
            });
        }

        return new DiagnosticModels.ObjectFieldLayout
        {
            ObjectAddress = $"0x{address:X16}",
            TypeName = resolved.Type.Name ?? "Unknown",
            Mt = $"0x{resolved.Type.MethodTable:X16}",
            TotalSize = (long)resolved.Size,
            Fields = fields,
        };
    }

    // ──────────────────────── Data Owner Finder ────────────────────────

    /// <summary>
    /// Finds the ClrObject that contains the given address in its memory
    /// footprint (obj.Address &lt;= address &lt; obj.Address + obj.Size).
    /// Works for any address, not just object starts.
    /// </summary>
    /// <param name="address">Any address in the dump.</param>
    /// <returns>The containing ClrObject and the byte offset within it, or null.</returns>
    public (ClrObject ContainingObject, long OffsetWithinObject)? FindContainingObject(ulong address)
    {
        foreach (var obj in Objects)
        {
            if (obj.Address <= address && address < obj.Address + obj.Size)
            {
                return (obj, (long)(address - obj.Address));
            }
        }

        return null;
    }

    /// <summary>
    /// Partitions a byte range into sub-regions classified by the objects
    /// that own each portion.
    /// </summary>
    /// <param name="startAddress">The start address of the memory range.</param>
    /// <param name="length">The length of the memory range in bytes.</param>
    /// <returns>List of classified memory regions.</returns>
    public List<DiagnosticModels.MemoryRegion> PartitionMemoryRange(ulong startAddress, int length)
    {
        var regions = new List<DiagnosticModels.MemoryRegion>();
        if (length <= 0) return regions;

        ulong endAddress = startAddress + (ulong)length - 1; // inclusive end
        ulong cursor = startAddress;

        // Get all objects that overlap with this range, sorted by address
        var overlappingObjects = new List<(ulong Address, ulong Size, string TypeName, ClrObject Obj)>();
        foreach (var obj in Objects)
        {
            ulong objEnd = obj.Address + obj.Size - 1;
            // Check overlap: obj range intersects [startAddress, endAddress]
            if (obj.Address <= endAddress && objEnd >= startAddress)
            {
                overlappingObjects.Add((obj.Address, obj.Size, obj.Type?.Name ?? "Free", obj));
            }
        }
        overlappingObjects.Sort((a, b) => a.Address.CompareTo(b.Address));

        while (cursor <= endAddress)
        {
            // Find the object that starts at or before cursor and extends past it
            var containing = overlappingObjects.FirstOrDefault(o =>
                o.Address <= cursor && cursor < o.Address + o.Size);

            if (containing.Address != 0)
            {
                ulong regionEnd = Math.Min(endAddress, containing.Address + containing.Size - 1);
                int regionLength = (int)(regionEnd - cursor + 1);
                long offsetInObj = (long)(cursor - containing.Address);

                string kind;
                if (cursor == containing.Address)
                    kind = "ObjectHeader";
                else if (containing.Obj.IsArray)
                    kind = "ArrayData";
                else if (containing.TypeName == "System.String")
                    kind = "StringData";
                else
                    kind = "InstanceField";

                regions.Add(new DiagnosticModels.MemoryRegion
                {
                    Offset = (int)(cursor - startAddress),
                    Length = regionLength,
                    Kind = kind,
                    ObjectAddress = $"0x{containing.Address:X16}",
                    ObjectTypeName = containing.TypeName,
                    ObjectSize = (long)containing.Size,
                    OffsetWithinObject = offsetInObj,
                });

                cursor = regionEnd + 1;
            }
            else
            {
                // Check if this is a free block between objects
                // Find the next object after cursor
                var nextObj = overlappingObjects.FirstOrDefault(o => o.Address > cursor);
                ulong gapEnd;
                if (nextObj.Address != 0)
                    gapEnd = Math.Min(endAddress, nextObj.Address - 1);
                else
                    gapEnd = endAddress;

                int gapLength = (int)(gapEnd - cursor + 1);
                regions.Add(new DiagnosticModels.MemoryRegion
                {
                    Offset = (int)(cursor - startAddress),
                    Length = gapLength,
                    Kind = "FreeBlock",
                });

                cursor = gapEnd + 1;
            }
        }

        return regions;
    }

    // ──────────────────────── Address Resolution ────────────────────────

    /// <summary>
    /// Finds a ClrObject at the exact given start address, or null if none exists.
    /// </summary>
    private ClrObject? FindObjectAtAddress(ulong address)
    {
        foreach (var obj in Objects)
            if (obj.Address == address) return obj;
        return null;
    }
}

