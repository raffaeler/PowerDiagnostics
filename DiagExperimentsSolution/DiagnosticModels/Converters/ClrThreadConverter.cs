using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Microsoft.Diagnostics.Runtime;

namespace DiagnosticModels.Converters;

public class ClrThreadConverter : JsonConverter<ClrThread>
{
    public override ClrThread Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }

    public override void Write(Utf8JsonWriter writer, ClrThread value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WriteString("Address", value.Address.ToString("X16"));
        writer.WriteNumber("ManagedThreadId", value.ManagedThreadId);
        writer.WriteNumber("OSThreadId", value.OSThreadId);
        writer.WriteBoolean("IsFinalizer", value.IsFinalizer);
        writer.WriteBoolean("IsAlive", value.IsAlive);

        if (value.CurrentException is not null)
        {
            writer.WritePropertyName("CurrentException");
            JsonSerializer.Serialize(writer, value.CurrentException, options);
        }

        writer.WriteEndObject();
    }
}
