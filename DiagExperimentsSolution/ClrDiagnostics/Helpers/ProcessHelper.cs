using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace ClrDiagnostics.Helpers;

/// <summary>
/// Provides process-lookup utilities backed by an <see cref="IProcessProvider"/>,
/// enabling unit testing through mock implementations.
/// </summary>
public class ProcessHelper
{
    private readonly IProcessProvider _processProvider;

    public ProcessHelper(IProcessProvider processProvider)
    {
        _processProvider = processProvider;
    }

    /// <summary>Default instance using the real <see cref="ProcessProvider"/>.</summary>
    public static ProcessHelper Default { get; } = new ProcessHelper(new ProcessProvider());

    public Process? GetProcess(string processName)
    {
        var pss = _processProvider.GetProcessesByName(processName);
        if (pss.Length != 1)
        {
            return null;
        }

        return pss.Single();
    }

    public Process? GetOrStartProcess(string processName, string filename)
    {
        var pss = _processProvider.GetProcessesByName(processName);
        if (pss.Length == 0)
        {
            return _processProvider.Start(new ProcessStartInfo(filename));
        }

        if (pss.Length == 1)
        {
            return pss.Single();
        }

        return null;
    }

    public IList<Process> GetDotnetProcesses()
    {
        var processes = _processProvider.GetPublishedProcesses()
            .OrderBy(p => p)
            .Select(p => _processProvider.GetProcessById(p))
            .Where(p => p != null)
            .Select(p => p!)
            .ToList();
        return processes;
    }
}

