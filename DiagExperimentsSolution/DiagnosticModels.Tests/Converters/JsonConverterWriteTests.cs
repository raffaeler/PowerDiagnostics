using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using DiagnosticModels.Converters;

using Microsoft.Diagnostics.Runtime;

namespace DiagnosticModels.Tests.Converters;

/// <summary>
/// Tests for JSON converter Read (deserialization) and basic converter infrastructure.
/// Write (serialization) tests require real ClrMD objects, which are mostly structs
/// in ClrMD v4 that cannot be mocked with NSubstitute — covered by integration tests against dump files.
/// </summary>
public class JsonConverterWriteTests
{
    private static void CallReadOn<T>(JsonConverter<T> converter)
    {
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes("{}"));
        converter.Read(ref reader, typeof(T), JsonSerializerOptions.Default);
    }

    [Fact]
    public void ClrObjectConverter_Read_ThrowsNotImplementedException()
        => Assert.Throws<NotImplementedException>(() => CallReadOn(new ClrObjectConverter()));

    [Fact]
    public void ClrTypeConverter_Read_ThrowsNotImplementedException()
        => Assert.Throws<NotImplementedException>(() => CallReadOn(new ClrTypeConverter()));

    [Fact]
    public void ClrModuleConverter_Read_ThrowsNotImplementedException()
        => Assert.Throws<NotImplementedException>(() => CallReadOn(new ClrModuleConverter()));

    [Fact]
    public void ClrRootConverter_Read_ThrowsNotImplementedException()
        => Assert.Throws<NotImplementedException>(() => CallReadOn(new ClrRootConverter()));

    [Fact]
    public void ClrStackFrameConverter_Read_ThrowsNotImplementedException()
        => Assert.Throws<NotImplementedException>(() => CallReadOn(new ClrStackFrameConverter()));

    [Fact]
    public void ClrInstanceFieldConverter_Read_ThrowsNotImplementedException()
        => Assert.Throws<NotImplementedException>(() => CallReadOn(new ClrInstanceFieldConverter()));

    [Fact]
    public void ClrStaticFieldConverter_Read_ThrowsNotImplementedException()
        => Assert.Throws<NotImplementedException>(() => CallReadOn(new ClrStaticFieldConverter()));

    [Fact]
    public void ClrThreadConverter_Read_ThrowsNotImplementedException()
        => Assert.Throws<NotImplementedException>(() => CallReadOn(new ClrThreadConverter()));

    [Fact]
    public void ClrExceptionConverter_Read_ThrowsNotImplementedException()
        => Assert.Throws<NotImplementedException>(() => CallReadOn(new ClrExceptionConverter()));
}
