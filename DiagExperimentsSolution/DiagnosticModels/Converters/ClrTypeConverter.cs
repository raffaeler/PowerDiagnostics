using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Microsoft.Diagnostics.Runtime;

namespace DiagnosticModels.Converters;

public class ClrTypeConverter : JsonConverter<ClrType>
{
    public override ClrType? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }

    public override void Write(Utf8JsonWriter writer, ClrType value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        WriteSafeString(writer, "Name", () => value.Name);
        WriteSafeString(writer, "Address", () => value.MethodTable.ToString("X16"));
        WriteSafe(writer, "IsFree", () => writer.WriteBoolean("IsFree", value.IsFree));
        WriteSafe(writer, "AssemblyLoadContextAddress", () => writer.WriteString("AssemblyLoadContextAddress", value.AssemblyLoadContextAddress.ToString("X16")));

        if (value.Module is not null)
        {
            writer.WritePropertyName("Module");
            try
            {
                JsonSerializer.Serialize(writer, value.Module, options);
            }
            catch (Exception ex)
            {
                // Module access can fail for runtime-generated / dynamic types.
                // Write a safe fallback to avoid breaking the entire response.
                writer.WriteStartObject();
                writer.WriteString("Name", $"(error: {ex.Message})");
                writer.WriteString("AssemblyName", "");
                writer.WriteString("Address", "0000000000000000");
                writer.WriteString("Size", "0");
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

    private static void WriteSafe(Utf8JsonWriter writer, string propertyName, Action writeAction)
    {
        try
        {
            writeAction();
        }
        catch
        {
            // Skip the property on error — already covered by WriteSafeString above
        }
    }
}
