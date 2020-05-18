using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading;

using Microsoft.Diagnostics.Runtime;
using System.IO;
using TestConsole.Helpers;

// TODO: dispose pattern
// TODO: symbols

namespace TestConsole.LinqToDump
{
    public class DumpSource : IDisposable
    {
        private DataTarget _dataTarget;
        private TimeSpan _timeout = TimeSpan.FromSeconds(5);
        private FileInfo _clrLocation;
        private ClrInfo _clrInfo;
        private ClrRuntime _clrRuntime;
        private DacInfo _dacInfo;

        private GCRoot _gcroot;
        private CancellationTokenSource _tokenSource = new CancellationTokenSource();
        private IList<ClrObject> _cachedAllObjects;

        public DumpSource(string filename)
        {
            _dataTarget = DataTarget.LoadDump(filename);
            Initialize();
        }

        public DumpSource(int pid)
        {
            //_dataTarget = DataTarget.AttachToProcess(pid, true);
            _dataTarget = DataTarget.CreateSnapshotAndAttach(pid);

            //_dataTarget.CacheOptions.CacheFieldNames = StringCaching.Cache;
            //_dataTarget.CacheOptions.CacheFields = true;
            //_dataTarget.CacheOptions.CacheMethodNames = StringCaching.Cache;
            //_dataTarget.CacheOptions.CacheMethods = true;
            //_dataTarget.CacheOptions.CacheTypeNames = StringCaching.Cache;
            //_dataTarget.CacheOptions.CacheTypes = true;

            Initialize();
        }

        private void Initialize()
        {
            if (_dataTarget.ClrVersions.Length == 0)
            {
                throw new Exception("No compatible CLR has been found on this machine");
            }

            if (_dataTarget.ClrVersions.Length > 1)
            {
                Console.WriteLine("Multiple compatible CLR have been found on this machine, picking the first of the following list");
                foreach (var version in _dataTarget.ClrVersions)
                {
                    Console.WriteLine("CLR Version: " + version.Version);
                    var dacInfo = version.DacInfo;
                    var moduleInfo = version.ModuleInfo;
                    Console.WriteLine("Filesize:  {0:X}", moduleInfo.IndexFileSize);
                    Console.WriteLine("Timestamp: {0:X}", moduleInfo.IndexTimeStamp);
                    Console.WriteLine("Dac File:  {0}", moduleInfo.FileName);
                    Console.WriteLine();
                }
            }

            _clrInfo = _dataTarget.ClrVersions[0];
            _clrLocation = new FileInfo(_clrInfo.DacInfo.LocalDacPath);
            _clrRuntime = _clrInfo.CreateRuntime();
            _dacInfo = _clrInfo.DacInfo;

            PrepareGCRootCache();
        }

        private void PrepareGCRootCache()
        {
            Token = _tokenSource.Token;
            _gcroot = new GCRoot(_clrRuntime.Heap);

            _gcroot.ProgressUpdated += delegate (GCRoot source, int processed) //long current, long total)
            {
                //var percent = total == 0 ? 0 : (int)(100 * current / (float)total);
                //OnGCRoot?.Invoke((current, total, percent, token));
                //OnGCRoot?.Invoke((processed, token));
                //OnGCRoot?.Invoke((processed, token));

            };
        }

        public bool CacheAllObjects { get; set; } = true;
        public CancellationToken Token { get; private set; }
        public void Cancel()
        {
            _tokenSource.Cancel();
            RenewCancellationToken();
        }

        private void RenewCancellationToken()
        {
            if (_tokenSource != null) _tokenSource.Dispose();
            _tokenSource = new CancellationTokenSource();
            Token = _tokenSource.Token;
        }

        public void Dispose()
        {
            if (_dataTarget != null)
            {
                _dataTarget.Dispose();
                _dataTarget = null;
            }
        }

        public IDataReader DataReader => _dataTarget.DataReader;

        public ClrAppDomain AppDomain => _clrRuntime.AppDomains.Single();  // .NET Core
        public IEnumerable<ClrModule> Modules => _clrRuntime.EnumerateModules();
        public IEnumerable<ClrHandle> Handles => _clrRuntime.EnumerateHandles();
        public IEnumerable<ClrThread> Threads => _clrRuntime.Threads;

        // Heap
        public IEnumerable<IClrRoot> Roots => _clrRuntime.Heap.EnumerateRoots();
        public IEnumerable<IClrRoot> FinalizerRoots => _clrRuntime.Heap.EnumerateFinalizerRoots();
        public IEnumerable<ClrObject> FinalizableObjects => _clrRuntime.Heap.EnumerateFinalizableObjects();
        public IEnumerable<ClrObject> Objects
        {
            get
            {
                var result = _clrRuntime.Heap.EnumerateObjects();
                if (CacheAllObjects)
                {
                    if (_cachedAllObjects == null) _cachedAllObjects = result.ToList();
                    return _cachedAllObjects;
                }

                return result;
            }
        }
        public IEnumerable<ClrObject> ObjectReferences(ClrObject @object)
        {
            return _clrRuntime.Heap.EnumerateObjectReferences(@object.Address, @object.Type, false, true);
        }

        public IEnumerable<ClrReference> ObjectReferencesWithFields(ClrObject @object)
        {
            return _clrRuntime.Heap.EnumerateReferencesWithFields(@object.Address, @object.Type, false, true);
        }

        public IEnumerable<GCRootPath> RootPaths(ClrObject @object)
        {
            return _gcroot.EnumerateGCRoots(@object.Address, false, Token);
        }

        public IEnumerable<LinkedList<ClrObject>> PathsAmong(ClrObject source, ClrObject target)
        {
            return _gcroot.EnumerateAllPaths(source.Address, target.Address, false, Token);
        }

        public IDictionary<AllocatorInfo, List<ClrObject>> GetAllObjectsGroupedByAllocator(Func<ClrObject, bool> predicate)
        {
            return _clrRuntime.Heap.EnumerateObjects()
                .Where(predicate)
                .Select(o => (Object: o,
                              AllocatorInfo: Create(o.Type.LoaderAllocatorHandle)))
                .GroupBy(g => g.AllocatorInfo, g => g.Object, new AllocatorInfoComparer())
                .ToDictionary(g => g.Key, g => g.ToList());

            AllocatorInfo Create(ulong allocatorHandle)
            {
                IDataReader dataReader = _dataTarget.DataReader;
                if (allocatorHandle == 0) return AllocatorInfo.Default;
                var loaderAllocatorAddress = dataReader.ReadPointer(allocatorHandle);
                var allocator = _clrRuntime.Heap.GetObject(loaderAllocatorAddress);
                return AllocatorInfo.Create(allocator);

                //var allocatorScout = allocator.ReadObjectField("m_scout");
                //var ptrNativeLoaderAllocator = allocatorScout.ReadValueTypeField("m_nativeLoaderAllocator");

                ////var m_methodInstantiations = allocator.ReadValueTypeField("m_methodInstantiations");
                ////var m_slotsUsed = allocator.ReadValueTypeField("m_slotsUsed");
                //var reader = _clrRuntime.DataTarget.DataReader;
                //var ptrValue = reader.ReadPointer(ptrNativeLoaderAllocator.Address);
                //var buf = new Span<byte>(new byte[1024]);
                //var mem = reader.Read(ptrValue, buf, out int read);
            }
        }

        public IEnumerable<(ClrStaticField, ClrObject)> GetStaticFields()
        {
            return _clrRuntime
                .GetConstructedTypeDefinitions(t => t.StaticFields.Length > 0)
                .SelectMany(t => t.StaticFields)
                .Where(f => f.IsObjectReference)
                .Select(f => (field: f, value: f.ReadObject(AppDomain)))
                .Where(t => !t.value.IsNull);
        }

        public IEnumerable<(ClrStaticField, ClrObject, UInt64)> GetStaticFieldsWithGraphSize()
        {
            return _clrRuntime
                .GetConstructedTypeDefinitions(t => t.StaticFields.Length > 0)
                .SelectMany(t => t.StaticFields)
                .Where(f => f.IsObjectReference)
                .Select(f => (field: f, value: f.ReadObject(AppDomain)))
                .Select(t => (field: t.field, value: t.value, size: t.value.GetGraphSize()))
                .Where(t => !t.value.IsNull)
                .OrderByDescending(t => t.size);
        }

        public IEnumerable<(ClrStaticField, ClrObject, UInt64, IEnumerable<ClrGraphNode>)> GetStaticFieldsWithGraphAndSize()
        {
            return _clrRuntime
                .GetConstructedTypeDefinitions(t => t.StaticFields.Length > 0)
                .SelectMany(t => t.StaticFields)
                .Where(f => f.IsObjectReference)
                .Select(f => (field: f, value: f.ReadObject(AppDomain)))
                .Select(t => (field: t.field, value: t.value, size: t.value.GetGraphSize(),
                                graph: ClrGraph.CreateForChildren(t.value)))
                .Where(t => !t.value.IsNull)
                .OrderByDescending(t => t.size);
        }

        public Dictionary<string, int> GetDuplicateStrings(int minCount = 2)
        {
            return Objects
                .Where(o => o.Type.ElementType == ClrElementType.String)
                .Select(o => o.GetStringValue(int.MaxValue))
                .Where(s => !string.IsNullOrEmpty(s))
                .GroupBy(s => s)
                .Select(s => (str: s.Key, count: s.Count()))
                .Where(t => t.count >= minCount)
                .ToDictionary(t => t.str, t => t.count);
        }

        public IEnumerable<ClrObject> GetObjectsBySize(long minSize = 1024)
        {
            return Objects
                .Where(o => o.Size > (ulong)minSize)
                .OrderByDescending(o => o.Size);
        }

        public IEnumerable<(ClrObject, string)> GetStringsBySize(long minSize = 1024)
        {
            return Objects
                .Where(o => o.Type.ElementType == ClrElementType.String && o.Size > (ulong)minSize)
                .Select(o => (@object: o, @string: o.GetStringValue(int.MaxValue)))
                .Where(t => !string.IsNullOrEmpty(t.@string))
                .OrderByDescending(t => t.@string.Length);
        }

        // equivalent to SOS dumpheap -stat
        public IEnumerable<(ClrType type, List<ClrObject> objects, long size)> DumpHeapStat(long minTotalSize = 1024)
        {
            return Objects
                .GroupBy(o => o.Type, o => o)
                .Select(o => (type: o.Key, objects: o.ToList(), totalSize: o.Sum(s => (long)s.Size)))
                .Where(t => t.totalSize > minTotalSize)
                .OrderBy(t => t.totalSize);
        }

    }
}
