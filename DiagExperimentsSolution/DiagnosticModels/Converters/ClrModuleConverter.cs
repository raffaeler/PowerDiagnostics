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

        writer.WriteString("Name", value.Name);
        writer.WriteString("AssemblyName", value.AssemblyName);
        writer.WriteString("Address", value.Address.ToString("X16"));
        writer.WriteString("Size", value.Size.ToString());

        writer.WriteEndObject();
    }
}
