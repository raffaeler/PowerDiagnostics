namespace DiagnosticModels;

/// <summary>
/// Describes an object that holds a reference to a target object,
/// including which field (static or instance) contains the reference.
/// </summary>
public class GcReferenceInfo
{
    /// <summary>Hex address of the referencing object.</summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>Type name of the referencing object.</summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>Name of the field that holds the reference.</summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>True if the reference is from a static field; false for instance fields.</summary>
    public bool IsStatic { get; set; }
}
