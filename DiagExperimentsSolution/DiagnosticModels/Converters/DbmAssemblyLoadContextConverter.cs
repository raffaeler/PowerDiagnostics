using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DiagnosticModels.Converters;

public class DbmAssemblyLoadContextConverter : JsonConverter<DbmAssemblyLoadContext>
{
    public override DbmAssemblyLoadContext? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }

    public override void Write(Utf8JsonWriter writer, DbmAssemblyLoadContext value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("Address", value.Address ?? "0000000000000000");
        writer.WriteString("Name", value.Name);
        writer.WriteBoolean("IsDefault", value.IsDefault);
        writer.WriteEndObject();
    }
}
