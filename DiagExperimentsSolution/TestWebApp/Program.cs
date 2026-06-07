using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

using CustomEventSource;

using Microsoft.Extensions.Hosting;

using TestWebApp;
using TestWebApp.Configurations;
using TestWebApp.Models;
using TestWebApp.Services;

var myself = Process.GetCurrentProcess();
Console.WriteLine();
Console.WriteLine();
Console.WriteLine();
Console.WriteLine($"Process {myself.ProcessName} Id: {myself.Id}");
Console.WriteLine();
Console.WriteLine();

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<GeneralConfig>(builder.Configuration.GetSection("GeneralConfig"));

builder.Services.AddSingleton(CustomHeaderEventSource.Instance);

builder.Services.AddSingleton<CpuStressService>();
builder.Services.AddSingleton<MemoryPressureService>();
builder.Services.AddSingleton<SimpleStateService>();
builder.Services.AddSingleton<AddonService>();

builder.Services.AddHostedService<Worker>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return context.Response.WriteAsync("An unexpected server error occurred.");
        });
    });
    app.UseHsts();
}

app.UseRequestHook();

app.UseRouting();

app.MapGet("/Index", () => Results.Text("TestWebApp API is running."));

app.MapGet("/api/test", () =>
{
    var sources = EventSource.GetSources()
        .Select(s => $"{s.Name} {s.IsEnabled()}")
        .ToArray();
    return Results.Ok(sources);
});

app.MapPost("/api/test/simplepost", async (HttpRequest request, ILoggerFactory loggerFactory) =>
{
    _ = await ReadBodyAsync(request);
    var logger = loggerFactory.CreateLogger("TestEndpoints");
    logger.LogInformation("SimplePost");
    return Results.Ok();
});

app.MapPost("/api/test/exceptiononpost", async (HttpRequest request, ILoggerFactory loggerFactory, SimpleStateService simpleState) =>
{
    _ = await ReadBodyAsync(request);
    var logger = loggerFactory.CreateLogger("TestEndpoints");
    logger.LogInformation("ExceptionOnPost");
    if (simpleState.Next() % 250 == 0)
    {
        throw new DemoException("Throwing a bad exception");
    }

    return Results.Ok();
});

app.MapPost("/api/test/slowpost", async (HttpRequest request, ILoggerFactory loggerFactory, SimpleStateService simpleState) =>
{
    _ = await ReadBodyAsync(request);
    var logger = loggerFactory.CreateLogger("TestEndpoints");
    logger.LogInformation("SlowPost");
    if (simpleState.Next() % 4 == 0)
    {
        Thread.Sleep(500);
    }

    return Results.Ok();
});

app.MapPost("/api/test/leakblob", async (HttpRequest request, ILoggerFactory loggerFactory, MemoryPressureService memoryPressureService) =>
{
    _ = await ReadBodyAsync(request);
    var logger = loggerFactory.CreateLogger("TestEndpoints");
    logger.LogInformation("LeakBlobPost");
    memoryPressureService.AllocateArray();
    return Results.Ok();
});

app.MapPost("/api/test/leakgraph", async (HttpRequest request, ILoggerFactory loggerFactory, MemoryPressureService memoryPressureService) =>
{
    _ = await ReadBodyAsync(request);
    var logger = loggerFactory.CreateLogger("TestEndpoints");
    logger.LogInformation("LeakGraphPost");
    memoryPressureService.AllocateGraphRoots();
    return Results.Ok();
});

app.MapPost("/api/test/freeleaks", async (HttpRequest request, ILoggerFactory loggerFactory, MemoryPressureService memoryPressureService) =>
{
    _ = await ReadBodyAsync(request);
    var logger = loggerFactory.CreateLogger("TestEndpoints");
    logger.LogInformation("FreeLeaksPost");
    memoryPressureService.FreeAll();
    return Results.Ok();
});

app.MapPost("/api/test/gccollect", async (HttpRequest request, ILoggerFactory loggerFactory, MemoryPressureService memoryPressureService) =>
{
    _ = await ReadBodyAsync(request);
    var logger = loggerFactory.CreateLogger("TestEndpoints");
    logger.LogInformation("GcCollectPost");
    memoryPressureService.GCCollect();
    return Results.Ok();
});

app.MapPost("/api/test/cpustress", async (HttpRequest request, ILoggerFactory loggerFactory, CpuStressService cpuStressService) =>
{
    _ = await ReadBodyAsync(request);
    var logger = loggerFactory.CreateLogger("TestEndpoints");
    logger.LogInformation("CpuStressPost");
    cpuStressService.CpuLoad(250_000);
    return Results.Ok();
});

app.Run();

static async Task<string> ReadBodyAsync(HttpRequest request)
{
    using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
    return await reader.ReadToEndAsync();
}

