using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

using Microsoft.Diagnostics.Runtime;

// ClrType.GetArrayLength -> ClrObject.AsArray().Length
// ClrType.GetEnumName -> ClrType.AsEnum()...
// ClrType.GetValue -> maybe ClrHeap.GetObject
// ClrHeap.ReadMemory -> maybe DataTarget.DataReader.Read

namespace TestConsole.Helpers
{
    public class DumpHelper : IDisposable
    {
        private DataTarget _dataTarget;
        private TimeSpan _timeout = TimeSpan.FromSeconds(5);
        private FileInfo _clrLocation;
        private ClrInfo _clrInfo;
        private ClrRuntime _clrRuntime;
        private DacInfo _dacInfo;
        private GCRoot _gcroot;
        private CancellationTokenSource _tokenSource = new CancellationTokenSource();

        public DumpHelper(string filename)
        {
            _dataTarget = DataTarget.LoadDump(filename);
            Initialize();
        }

        public DumpHelper(int pid)
        {
            _dataTarget = DataTarget.AttachToProcess(pid, true);
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
            var token = _tokenSource.Token;
            _gcroot = new GCRoot(_clrRuntime.Heap);

            _gcroot.ProgressUpdated += delegate (GCRoot source, int processed) //long current, long total)
            {
                //var percent = total == 0 ? 0 : (int)(100 * current / (float)total);
                //OnGCRoot?.Invoke((current, total, percent, token));
                OnGCRoot?.Invoke((processed, token));
            };
        }

        //public Action<(long inspected, long total, int percent, CancellationToken token)> OnGCRoot { get; set; }
        public Action<(long processed, CancellationToken token)> OnGCRoot { get; set; }

        public void Dispose()
        {
            if (_dataTarget != null)
            {
                _dataTarget.Dispose();
                _dataTarget = null;
            }
        }

        public IEnumerable<ModuleInfo> GetModules() => _dataTarget.EnumerateModules();
        public IList<ClrAppDomain> AppDomains => _clrRuntime.AppDomains;
        public ClrAppDomain MainAppDomain => AppDomains.First();
        public IList<ClrModule> ModulesInMainAppDomain => MainAppDomain.Modules;
        public IList<ClrThread> Threads => _clrRuntime.Threads;

        //public ulong GetPointer(object obj)
        //{
        //    //_clrRuntime.Heap.ReadPointer()
        //}

        public IEnumerable<ClrObject> GetAllObjects() => GetAllObjects(_ => true);

        public IEnumerable<ClrObject> GetAllObjects(Func<ClrObject, bool> predicate)
        {
            if (!_clrRuntime.Heap.CanWalkHeap) yield break;

            foreach (var obj in _clrRuntime.Heap.EnumerateObjects().Where(predicate))
            {
                yield return obj;
            }
        }

            //var asmLoadContexts = GetObjectsOfType("System.Runtime.Loader.AssemblyLoadContext", false)
            //    .ToList();


        public void GetAllObjectsGroupedByAllocator(Func<ClrObject, bool> predicate)
        {
            var grouping = _clrRuntime.Heap.EnumerateObjects()
                .Where(predicate)
                .Select(o => (Object: o,
                              AllocatorInfo: Create(o.Type.LoaderAllocatorHandle)))
                .GroupBy(g => g.AllocatorInfo, new AllocatorInfoComparer())
                .ToList();


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

        public IEnumerable<GCRootPath> EnumerateRoots(ulong address)
        {
            foreach (var root in _gcroot.EnumerateGCRoots(address, false, _tokenSource.Token))
            {
                yield return root;
            }
        }

        public IList<LinkedList<ClrObject>> FindAllPaths(ulong target, ulong source)
        {
            return _gcroot.EnumerateAllPaths(source, target, false, _tokenSource.Token).ToList();
        }

        public IEnumerable<ClrReference> FindReferences(ClrObject source)
        {
            return _clrRuntime.Heap.EnumerateReferencesWithFields(source.Address, source.Type, false, true);
        }
    }

}
