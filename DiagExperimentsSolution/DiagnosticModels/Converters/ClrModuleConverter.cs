using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Microsoft.Diagnostics.Runtime;

namespace DiagnosticModels.Converters;

public class ClrModuleConverter : JsonConverter<ClrModule>
{
    public override ClrModule Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }

    public override void Write(Utf8JsonWriter writer, ClrModule value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        WriteSafeString(writer, "Name", () => value.Name);
        WriteSafeString(writer, "AssemblyName", () => value.AssemblyName);
        WriteSafeString(writer, "Address", () => value.Address.ToString("X16"));
        WriteSafeString(writer, "Size", () => value.Size.ToString());

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
