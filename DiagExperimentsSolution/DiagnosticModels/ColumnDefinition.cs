namespace DiagnosticModels;

/// <summary>
/// Describes a single column in a diagnostic DataGrid.
/// Mirrors the WPF UIGridColumn concept for client-side rendering.
/// </summary>
public class ColumnDefinition
{
    /// <summary>Column header text.</summary>
    public string Header { get; set; } = string.Empty;

    /// <summary>Property path for value binding (e.g., "Type.Name", "Thread.ManagedThreadId").</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Optional format string (e.g., "0:X16" for hex, "0:N0" for comma-separated numbers).</summary>
    public string? Format { get; set; }

    /// <summary>If true, the column should be right-aligned.</summary>
    public bool AlignRight { get; set; }

    /// <summary>Optional tooltip path or static text.</summary>
    public string? Tooltip { get; set; }
}
