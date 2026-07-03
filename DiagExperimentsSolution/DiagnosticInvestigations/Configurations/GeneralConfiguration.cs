namespace DiagnosticInvestigations.Configurations;

public class GeneralConfiguration
{
    public int DebuggingSessionsExpirationMinutes { get; set; } = 10;
    public string DumpsFolder { get; set; } = @"H:\_dumps";
    public AnalyzerSettings AnalyzerSettings { get; set; } = new();
}
