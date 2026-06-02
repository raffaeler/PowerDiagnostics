using System.Diagnostics;

using Microsoft.Diagnostics.NETCore.Client;

namespace ClrDiagnostics.Helpers;

/// <summary>
/// Production implementation of <see cref="IProcessProvider"/> that delegates
/// to the real <see cref="Process"/> and <see cref="DiagnosticsClient"/> APIs.
/// </summary>
public class ProcessProvider : IProcessProvider
{
    public Process[] GetProcessesByName(string processName)
        => Process.GetProcessesByName(processName);

    public Process? Start(ProcessStartInfo startInfo)
        => Process.Start(startInfo);

    public IEnumerable<int> GetPublishedProcesses()
        => DiagnosticsClient.GetPublishedProcesses();

    public Process? GetProcessById(int pid)
    {
        try
        {
            return Process.GetProcessById(pid);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetProcessById failed (ghost process?): {ex.Message}");
            return null;
        }
    }
}
