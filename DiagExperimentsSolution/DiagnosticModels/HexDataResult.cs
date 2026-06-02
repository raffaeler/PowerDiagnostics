namespace DiagnosticModels;

/// <summary>
/// Raw byte content of a heap object, returned for hex viewer display.
/// </summary>
public class HexDataResult
{
    /// <summary>Hex address of the object (e.g., "0x000001A2B3C4D5E6").</summary>
    public string ObjectAddress { get; set; } = string.Empty;

    /// <summary>Type name of the object.</summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>Size of the object in bytes.</summary>
    public long Size { get; set; }

    /// <summary>Raw bytes encoded as base64 string.</summary>
    public string BytesBase64 { get; set; } = string.Empty;
}
