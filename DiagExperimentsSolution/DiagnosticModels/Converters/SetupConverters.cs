using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DiagnosticModels.Converters;

public static class SetupConverters
{
    public static JsonSerializerOptions CreateOptions()
        => new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters =
            {
                new ClrExceptionConverter(),
                new ClrInstanceFieldConverter(),
                new ClrModuleConverter(),
                new ClrObjectConverter(),
                new ClrRootConverter(),
                new ClrStackFrameConverter(),
                new ClrStaticFieldConverter(),
                new ClrThreadConverter(),
                new ClrTypeConverter(),
            }
        };
}
