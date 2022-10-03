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

        writer.WriteString("Name", value.Name);
        writer.WriteString("Address", value.MethodTable.ToString("X16"));
        writer.WriteBoolean("IsFree", value.IsFree);

        //writer.WriteBoolean("IsString", value.IsString);
        writer.WriteString("AssemblyLoadContextAddress", value.AssemblyLoadContextAddress.ToString("X16"));

        if (value.Module is not null)
        {
            writer.WritePropertyName("Module");
            JsonSerializer.Serialize(writer, value.Module, options);
        }

        writer.WriteEndObject();
    }
}
