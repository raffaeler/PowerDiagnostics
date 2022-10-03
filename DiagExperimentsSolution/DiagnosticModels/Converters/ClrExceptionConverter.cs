using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Microsoft.Diagnostics.Runtime;

namespace DiagnosticModels.Converters;

public class ClrExceptionConverter : JsonConverter<ClrException>
{
    public override ClrException Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }

    public override void Write(Utf8JsonWriter writer, ClrException value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WriteString("Address", value.Address.ToString("X16"));
        writer.WriteString("Message", value.Message);

        writer.WritePropertyName("StackTrace");
        JsonSerializer.Serialize(writer, value.StackTrace, options);

        //if (value.Thread is not null)
        //{
        //    writer.WritePropertyName("Thread");
        //    JsonSerializer.Serialize(writer, value.Thread, options);
        //}

        if (value.Type is not null)
        {
            writer.WritePropertyName("Type");
            JsonSerializer.Serialize(writer, value.Type, options);
        }

        if (value.Inner is not null)
        {
            writer.WritePropertyName("Inner");
            JsonSerializer.Serialize(writer, value.Inner, options);
        }

        writer.WriteEndObject();
    }
}
