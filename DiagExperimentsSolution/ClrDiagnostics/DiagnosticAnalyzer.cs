using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading;

using Microsoft.Diagnostics.Runtime;
using ClrDiagnostics.Helpers;
using Microsoft.Diagnostics.NETCore.Client;

namespace ClrDiagnostics;
public partial class DiagnosticAnalyzer : IDisposable
{
    private bool _isDisposed;
    private DataTarget _dataTarget;
    private TimeSpan _timeout = TimeSpan.FromSeconds(5);
    private FileInfo _clrLocation;
    private ClrInfo _clrInfo;
    private ClrRuntime _clrRuntime;
    private DebugLibraryInfo _debugLibraryInfo;
    private DirectoryInfo _dumpDirectory;
    private FileInfo? _dumpFile;

    private IList<ClrObject>? _cachedAllObjects;
    private IList<(ClrObject, ClrInstanceField, ulong)>? _objectsWithInstanceFields;
    private IList<(ClrObject, ClrStaticField, ulong)>? _objectsWithStaticFields;
    private CancellationTokenSource _tokenSource = new CancellationTokenSource();

    /// <summary>Parameterless constructor for NSubstitute proxy generation in tests.</summary>
#pragma warning disable CS8618 // Non-nullable fields uninitialized — test proxy only
    protected DiagnosticAnalyzer() { }
#pragma warning restore CS8618

    internal DiagnosticAnalyzer(DataTarget dataTarget, bool cacheObjects,
        DirectoryInfo dumpDirectory, FileInfo? dumpFile)
    {
        _dataTarget = dataTarget;
        CacheAllObjects = cacheObjects;
        _dumpDirectory = dumpDirectory;
        _dumpFile = dumpFile;
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
    }

    public bool ApplyNet10DatasStaticWorkaround { get; set; } = false;

    public static DiagnosticAnalyzer FromDump(int pid, bool cacheObjects = true)
    {
        var temp = Path.GetTempFileName();
        var client = new DiagnosticsClient(pid);
        client.WriteDump(DumpType.WithHeap, temp);
        var dataTarget = DataTarget.LoadDump(temp);

        return new DiagnosticAnalyzer(dataTarget, cacheObjects,
            new DirectoryInfo(Path.GetTempPath()), null);
    }

    public static DiagnosticAnalyzer FromDump(string filename, bool cacheObjects,
        params string[] additionalPdbs)
    {
        var dataTarget = DataTarget.LoadDump(filename);

        return new DiagnosticAnalyzer(dataTarget, cacheObjects,
            new DirectoryInfo(Path.GetDirectoryName(filename)!), new FileInfo(filename));
    }

    public static DiagnosticAnalyzer FromSnapshot(int pid, bool cacheObjects = true)
    {
        var dataTarget = DataTarget.CreateSnapshotAndAttach(pid);
        //dataTarget.BinaryLocator.FindBinary()
        return new DiagnosticAnalyzer(dataTarget, cacheObjects,
            new DirectoryInfo(Path.GetTempPath()), null);
    }

    public static DiagnosticAnalyzer? FromSnapshot(string processName, bool cacheObjects = true)
    {
        var process = ProcessHelper.Default.GetProcess(processName);
        if (process == null) return null;
        return FromSnapshot(process.Id, cacheObjects);
    }

    public static DiagnosticAnalyzer FromProcess(int pid, bool cacheObjects = true)
    {
        var dataTarget = DataTarget.AttachToProcess(pid, true);
        return new DiagnosticAnalyzer(dataTarget, cacheObjects,
            new DirectoryInfo(Path.GetTempPath()), null);
    }

    public static DiagnosticAnalyzer? FromProcess(string processName, bool cacheObjects = true)
    {
        var process = ProcessHelper.Default.GetProcess(processName);
        if (process == null) return null;
        return FromProcess(process.Id, cacheObjects);
    }

    public ClrRuntime ClrRuntime => _clrRuntime;
    public ClrHeap Heap => _clrRuntime.Heap;

    public bool CacheAllObjects { get; }


    public CancellationToken Token { get; private set; }

    public void Cancel()
    {
        _tokenSource.Cancel();
        RenewCancellationToken();
    }

    /// <summary>
    /// Resolves the <see cref="ClrType"/> for an object at the given address.
    /// Returns <see langword="null"/> if the address does not point to a valid object.
    /// </summary>
    public ClrType? GetObjectType(ulong address) => _clrRuntime.Heap.GetObjectType(address);

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
            _clrRuntime = null!;
        }

        if (_dataTarget != null)
        {
            _dataTarget.Dispose();
            _dataTarget = null!;
        }
    }

    /// <summary>
    /// Creates a new <see cref="GCRoot"/> instance for the given target address.
    /// v3/v4: GCRoot targets are specified at construction, not per-call.
    /// A new instance is created per call because <see cref="GCRoot"/> is not thread-safe.
    /// </summary>
    private GCRoot CreateGCRoot(ulong targetAddress)
    {
        return new GCRoot(_clrRuntime.Heap, new[] { targetAddress });
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

