using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Microsoft.Diagnostics.Runtime;
//using Microsoft.Diagnostics.Runtime.Interfaces;   // required by Microsoft.Diagnostics.Runtime version 3.0

namespace DiagnosticModels.Converters;

public class ClrRootConverter : JsonConverter<ClrRoot>
{
    public override ClrRoot Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }

    public override void Write(Utf8JsonWriter writer, ClrRoot value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        WriteSafeString(writer, "Address", () => value.Address.ToString("X16"));
        WriteSafe(writer, () => writer.WriteBoolean("IsPinned", value.IsPinned));
        WriteSafeString(writer, "RootKind", () => value.RootKind.ToString());

        writer.WritePropertyName("Object");
        try
        {
            JsonSerializer.Serialize(writer, value.Object, options);
        }
        catch (Exception)
        {
            // Object resolution can fail for roots pointing to invalid/unreadable memory.
            // Write null so the client can easily filter these roots out.
            writer.WriteNullValue();
        }

        writer.WriteEndObject();
    }

    private static void WriteSafeString(Utf8JsonWriter writer, string propertyName, Func<string> getValue)
    {
        try
        {
            writer.WriteString(propertyName, getValue());
        }
        catch
        {
            writer.WriteString(propertyName, "");
        }
    }

    private static void WriteSafe(Utf8JsonWriter writer, Action writeAction)
    {
        try
        {
            writeAction();
        }
        catch
        {
            // Skip property on error
        }
    }
}
