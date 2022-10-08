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

    public static void ConfigureOptions(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.Converters.Clear();
        options.Converters.Add(new ClrExceptionConverter());
        options.Converters.Add(new ClrInstanceFieldConverter());
        options.Converters.Add(new ClrModuleConverter());
        options.Converters.Add(new ClrObjectConverter());
        options.Converters.Add(new ClrRootConverter());
        options.Converters.Add(new ClrStackFrameConverter());
        options.Converters.Add(new ClrStaticFieldConverter());
        options.Converters.Add(new ClrThreadConverter());
        options.Converters.Add(new ClrTypeConverter());
    }
}
