namespace DiagnosticInvestigations.Configurations;

/// <summary>
/// Settings for the diagnostic analyzer behavior.
/// </summary>
public class AnalyzerSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether to apply the .NET 10
    /// static data workaround during analysis.
    /// </summary>
    public bool ApplyNet10DatasStaticWorkaround { get; set; }
}
