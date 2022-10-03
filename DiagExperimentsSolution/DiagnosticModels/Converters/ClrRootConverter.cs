using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Microsoft.Diagnostics.Runtime;

namespace DiagnosticModels.Converters;

public class ClrRootConverter : JsonConverter<IClrRoot>
{
    public override IClrRoot Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }

    public override void Write(Utf8JsonWriter writer, IClrRoot value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WriteString("Address", value.Address.ToString("X16"));
        writer.WriteBoolean("IsPinned", value.IsPinned);
        writer.WriteString("RootKind", value.RootKind.ToString());

        writer.WritePropertyName("Object");
        JsonSerializer.Serialize(writer, value.Object, options);

        writer.WriteEndObject();
    }
}
