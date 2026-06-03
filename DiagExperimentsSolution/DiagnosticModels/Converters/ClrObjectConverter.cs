using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Microsoft.Diagnostics.Runtime;

namespace DiagnosticModels.Converters;

public class ClrObjectConverter : JsonConverter<ClrObject>
{
    public override ClrObject Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }

    public override void Write(Utf8JsonWriter writer, ClrObject value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        WriteSafeString(writer, "Address", () => value.Address.ToString("X16"));

        // ClrObject.Size throws InvalidOperationException when Type is null
        // (incomplete type information). Check Type first to guard against this.
        if (value.Type is not null)
        {
            try
            {
                writer.WriteString("Size", value.Size.ToString());
            }
            catch
            {
                writer.WriteString("Size", "0");
            }
        }

        if (value.Type is not null)
        {
            writer.WritePropertyName("Type");
            try
            {
                JsonSerializer.Serialize(writer, value.Type, options);
            }
            catch (Exception ex)
            {
                // Type serialization failed (e.g., Module access issue).
                // Write a minimal safe type to avoid breaking the entire response.
                writer.WriteStartObject();
                writer.WriteString("Name", $"(error: {ex.Message})");
                writer.WriteString("Address", "0000000000000000");
                writer.WriteBoolean("IsFree", false);
                writer.WriteString("AssemblyLoadContextAddress", "0000000000000000");
                writer.WriteEndObject();
            }
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
}
