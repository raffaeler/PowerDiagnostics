using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Microsoft.Diagnostics.Runtime;

namespace DiagnosticModels.Converters;

public class ClrStaticFieldConverter : JsonConverter<ClrStaticField>
{
    public override ClrStaticField Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }

    public override void Write(Utf8JsonWriter writer, ClrStaticField value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WriteString("Name", value.Name);
        writer.WriteString("Size", value.Size.ToString());

        if (value.Type is not null)
        {
            writer.WritePropertyName("Type");
            JsonSerializer.Serialize(writer, value.Type, options);
        }

        if (value.ContainingType is not null)
        {
            writer.WritePropertyName("ContainingType");
            JsonSerializer.Serialize(writer, value.ContainingType, options);
        }

        writer.WriteEndObject();
    }
}
