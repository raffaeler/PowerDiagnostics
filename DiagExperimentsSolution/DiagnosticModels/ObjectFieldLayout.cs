namespace DiagnosticModels;

/// <summary>
/// Describes a field within a heap object, including its offset, type,
/// and whether it holds an object reference.
/// </summary>
public class FieldInfo
{
    /// <summary>Byte offset of this field from the object's start address.</summary>
    public int Offset { get; set; }

    /// <summary>Name of the field.</summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>CLR type name of the field.</summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>True if this field holds an object reference.</summary>
    public bool IsObjectReference { get; set; }

    /// <summary>Hex representation of the field's value (e.g., "0x00007FFA12345678").</summary>
    public string ValueHex { get; set; } = string.Empty;

    /// <summary>
    /// When IsObjectReference is true, the target object's address in hex.
    /// Null for value-type fields.
    /// </summary>
    public string? TargetAddressHex { get; set; }
}

/// <summary>
/// Layout of an object's fields with their offsets, types, and values,
/// enabling the frontend to annotate which bytes in a hex dump are references.
/// </summary>
public class ObjectFieldLayout
{
    /// <summary>Hex address of the object (e.g., "0x000001A2B3C4D5E6").</summary>
    public string ObjectAddress { get; set; } = string.Empty;

    /// <summary>Type name of the object.</summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>MethodTable address of the object's type (e.g., "0x00007FFA12345678").</summary>
    public string Mt { get; set; } = string.Empty;

    /// <summary>Total size of the object in bytes.</summary>
    public long TotalSize { get; set; }

    /// <summary>Fields of the object, ordered by offset.</summary>
    public List<FieldInfo> Fields { get; set; } = new();
}
