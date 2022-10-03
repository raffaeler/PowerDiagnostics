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

        writer.WriteString("Address", value.Address.ToString("X16"));
        writer.WriteString("Size", value.Size.ToString());

        if (value.Type is not null)
        {
            writer.WritePropertyName("Type");
            JsonSerializer.Serialize(writer, value.Type, options);
        }

        writer.WriteEndObject();
    }
}
