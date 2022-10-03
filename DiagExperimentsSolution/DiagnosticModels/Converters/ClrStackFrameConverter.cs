using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Microsoft.Diagnostics.Runtime;

namespace DiagnosticModels.Converters;

public class ClrStackFrameConverter : JsonConverter<ClrStackFrame>
{
    public override ClrStackFrame Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }

    public override void Write(Utf8JsonWriter writer, ClrStackFrame value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WriteString("FrameName", value.FrameName);
        writer.WriteString("Kind", value.Kind.ToString());
        writer.WriteString("StackPointer", value.StackPointer.ToString("X16"));

        writer.WriteString("MethodName", value.Method?.Name);
        writer.WriteString("MethodType", value.Method?.Type.Name);

        writer.WriteEndObject();
    }
}
