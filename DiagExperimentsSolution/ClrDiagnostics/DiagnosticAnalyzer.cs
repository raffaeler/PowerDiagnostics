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
        private DacInfo _dacInfo;

        private GCRoot _gcroot;
        private IList<ClrObject> _cachedAllObjects;
        private CancellationTokenSource _tokenSource = new CancellationTokenSource();

        private DiagnosticAnalyzer(DataTarget dataTarget, bool cacheObjects)
        {
            _dataTarget = dataTarget;
            CacheAllObjects = cacheObjects;

            if (_dataTarget.ClrVersions.Length == 0)
            {
                throw new Exception("No compatible CLR has been found on this machine");
            }

            if (_dataTarget.ClrVersions.Length > 1)
            {
                Debug.WriteLine("Multiple compatible CLR have been found on this machine, picking the first of the following list");
                foreach (var version in _dataTarget.ClrVersions)
                {
                    var dacFilename = version.DacInfo.PlatformSpecificFileName;
                    var moduleInfo = version.ModuleInfo;
                    Debug.WriteLine("CLR Version: " + version.Version);
                    Debug.WriteLine("Filesize:  {0:X}", moduleInfo.IndexFileSize);
                    Debug.WriteLine("Timestamp: {0:X}", moduleInfo.IndexTimeStamp);
                    Debug.WriteLine("Dac File:  {0}", dacFilename);
                    Debug.WriteLine("");
                }
            }

            _clrInfo = _dataTarget.ClrVersions[0];
            _clrLocation = new FileInfo(_clrInfo.DacInfo.LocalDacPath);
            _clrRuntime = _clrInfo.CreateRuntime();
            _dacInfo = _clrInfo.DacInfo;

            PrepareGCRootCache();
        }

        public static DiagnosticAnalyzer FromDump(string filename, bool cacheObjects = true)
        {
            var dataTarget = DataTarget.LoadDump(filename);
            return new DiagnosticAnalyzer(dataTarget, cacheObjects);
        }

        public static DiagnosticAnalyzer FromSnapshot(int pid, bool cacheObjects = true)
        {
            var dataTarget = DataTarget.CreateSnapshotAndAttach(pid);
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
