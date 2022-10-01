using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading;

using Microsoft.Diagnostics.Runtime;
using ClrDiagnostics.Helpers;

namespace ClrDiagnostics
{
    public partial class DiagnosticAnalyzer : IDisposable
    {
        private bool _isDisposed;
        private DataTarget _dataTarget;
        private TimeSpan _timeout = TimeSpan.FromSeconds(5);
        private FileInfo _clrLocation;
        private ClrInfo _clrInfo;
        private ClrRuntime _clrRuntime;
        private DebugLibraryInfo _debugLibraryInfo;

        private GCRoot _gcroot;
        private IList<ClrObject> _cachedAllObjects;
        private CancellationTokenSource _tokenSource = new CancellationTokenSource();

        private DiagnosticAnalyzer(DataTarget dataTarget, bool cacheObjects)
        {
            _dataTarget = dataTarget;
            CacheAllObjects = cacheObjects;
            //_dataTarget.BinaryLocator.FindBinary()

            if (_dataTarget.ClrVersions.Length == 0)
            {
                throw new Exception("[1] No compatible CLR has been found on this machine");
            }

            if (_dataTarget.ClrVersions.Length > 1)
            {
                Debug.WriteLine("Multiple compatible CLR have been found on this machine, picking the first of the following list");
                foreach (var clrInfo in _dataTarget.ClrVersions)
                {
                    Debug.WriteLine($"Version:        {clrInfo.Version}");
                    Debug.WriteLine($"IndexFileSize:  {clrInfo.IndexFileSize:X}");
                    Debug.WriteLine($"IndexTimeStamp: {clrInfo.IndexTimeStamp:X}");
                    foreach (var dli in clrInfo.DebuggingLibraries)
                    {
                        Debug.WriteLine($"    FileName:           {dli.FileName}");
                        Debug.WriteLine($"    TargetArchitecture: {dli.TargetArchitecture}");
                        Debug.WriteLine($"    Platform:           {dli.Platform}");
                        Debug.WriteLine($"    IndexFileSize:      {dli.IndexFileSize:X}");
                        Debug.WriteLine($"    IndexTimeStamp:     {dli.IndexTimeStamp:X}");
                        Debug.WriteLine("");
                    }

                    Debug.WriteLine("");
                }
            }

            _clrInfo = _dataTarget.ClrVersions[0];
            if (_clrInfo.DebuggingLibraries.Length == 0)
            {
                throw new Exception("[2] No compatible CLR has been found on this machine");
            }

            if(_clrInfo.DebuggingLibraries.Length > 1)
            {
                // TODO: match the current platform/architecture
                _debugLibraryInfo = _clrInfo.DebuggingLibraries[0];
            }
            else
            {
                _debugLibraryInfo = _clrInfo.DebuggingLibraries.Single();
            }

            if (_debugLibraryInfo.FileName == null)
            {
                throw new Exception($"The runtime used in the dump is Version:{_clrInfo.Version} Platform:{_debugLibraryInfo.Platform} Architecture:{_debugLibraryInfo.TargetArchitecture} and cannot be found on this installation");
            }

            _clrLocation = new FileInfo(_debugLibraryInfo.FileName);
            _clrRuntime = _clrInfo.CreateRuntime();

            PrepareGCRootCache();
        }

        public static DiagnosticAnalyzer FromDump(string filename, bool cacheObjects,
            params string[] additionalPdbs)
        {
            var dataTarget = DataTarget.LoadDump(filename, new CacheOptions()
            {
                // ...                
            });
            return new DiagnosticAnalyzer(dataTarget, cacheObjects);
        }

        public static DiagnosticAnalyzer FromSnapshot(int pid, bool cacheObjects = true)
        {
            var dataTarget = DataTarget.CreateSnapshotAndAttach(pid);
            //dataTarget.BinaryLocator.FindBinary()
            return new DiagnosticAnalyzer(dataTarget, cacheObjects);
        }

        public static DiagnosticAnalyzer FromSnapshot(string processName, bool cacheObjects = true)
        {
            var process = ProcessHelper.GetProcess(processName);
            if (process == null) return null;
            return FromSnapshot(process.Id, cacheObjects);
        }

        public static DiagnosticAnalyzer FromProcess(int pid, bool cacheObjects = true)
        {
            var dataTarget = DataTarget.AttachToProcess(pid, true);
            return new DiagnosticAnalyzer(dataTarget, cacheObjects);
        }

        public static DiagnosticAnalyzer FromProcess(string processName, bool cacheObjects = true)
        {
            var process = ProcessHelper.GetProcess(processName);
            if (process == null) return null;
            return FromProcess(process.Id, cacheObjects);
        }

        public Action<(long processed, CancellationToken token)> OnGCRoot { get; set; }
        public bool CacheAllObjects { get; }
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

        public void Close()
        {
            if (_clrRuntime != null)
            {
                _clrRuntime.Dispose();
                _clrRuntime = null;
            }

            if (_dataTarget != null)
            {
                _dataTarget.Dispose();
                _dataTarget = null;
            }
        }

        private void PrepareGCRootCache()
        {
            var token = _tokenSource.Token;
            _gcroot = new GCRoot(_clrRuntime.Heap);

            _gcroot.ProgressUpdated += delegate (GCRoot source, long processed)
            {
                OnGCRoot?.Invoke((processed, token));
            };
        }

        #region Dispose pattern
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~DiagnosticAnalyzer()
        {
            Dispose(false);
        }

        protected void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    OnDisposing();
                    Close();
                }

                _isDisposed = true;
            }
        }

        protected virtual void OnDisposing()
        {
        }
        #endregion
    }
}
