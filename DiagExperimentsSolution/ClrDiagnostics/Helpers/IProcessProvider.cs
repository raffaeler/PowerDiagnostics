using System.Diagnostics;

namespace ClrDiagnostics.Helpers;

/// <summary>
/// Abstraction over process-related system operations, enabling unit testing
/// of <see cref="ProcessHelper"/> via mock implementations.
/// </summary>
public interface IProcessProvider
{
    /// <summary>Wraps <see cref="Process.GetProcessesByName(string)"/>.</summary>
    Process[] GetProcessesByName(string processName);

    /// <summary>Wraps <see cref="Process.Start(string)"/>.</summary>
    Process? Start(ProcessStartInfo startInfo);

    /// <summary>Wraps <see cref="Microsoft.Diagnostics.NETCore.Client.DiagnosticsClient.GetPublishedProcesses()"/>.</summary>
    IEnumerable<int> GetPublishedProcesses();

    /// <summary>Wraps <see cref="Process.GetProcessById(int)"/>.</summary>
    Process? GetProcessById(int pid);
}
