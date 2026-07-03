using DiagnosticInvestigations;
using DiagnosticInvestigations.Configurations;

using DiagnosticModels.Converters;

using DiagnosticServer.Extensions;
using DiagnosticServer.Hubs;
using DiagnosticServer.Mcp;
using DiagnosticServer.Services;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

using Scalar.AspNetCore;

namespace DiagnosticServer;

public class Program
{
    public static async Task Main(string[] args)
    {
        const string corsPolicy = "CorsPolicy";

        // ── Stdio Mode ──
        // When --stdio is passed, run as a stdio-only MCP server (no web host).
        if (args.Contains("--stdio"))
        {
            await RunStdioMcpServer(args);
            return;
        }

        var builder = WebApplication.CreateBuilder(args);

        // Kestrel: allow large request bodies for dump file uploads
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = 2_147_483_648; // 2 GB
        });

        // FormOptions: allow large multipart form sections for dump file uploads
        builder.Services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = long.MaxValue; // no practical limit
        });

        // Configuration
        var generalSection = builder.Configuration.GetSection("General");
        builder.Services.Configure<GeneralConfiguration>(generalSection);

        // OpenAPI
        builder.Services.AddOpenApi();

        // SignalR
        builder.Services.AddSignalR();

        // CORS (required during front-end development with React at localhost:3000)
        builder.Services.AddCors(options =>
        {
            options.AddPolicy(corsPolicy, policy =>
            {
                policy
                    .AllowCredentials()
                    .WithOrigins("https://localhost:3000", "http://localhost:3000")
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        // Problem Details (RFC 7807)
        builder.Services.AddProblemDetails();

        // JSON: camelCase naming + custom converters for ClrMD types (ClrStackFrame, etc.)
        builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
        {
            SetupConverters.ConfigureOptions(options.SerializerOptions);
        });

        // Application Services
        builder.Services.AddSingleton<DebuggingSessionService>();
        builder.Services.AddHostedService<DebuggingSessionService>(
            provider => provider.GetRequiredService<DebuggingSessionService>());
        builder.Services.AddSingleton<QueriesService>();
        builder.Services.AddSingleton<InvestigationState>();

        // ── MCP Services ──
        builder.Services.AddMcpServices(builder.Configuration);
        builder.Services.AddMcpHttpServer();

        var app = builder.Build();

        // Middleware Pipeline

        // Global exception handler: returns ProblemDetails JSON
        app.UseExceptionHandler(exceptionHandlerApp =>
        {
            exceptionHandlerApp.Run(async context =>
            {
                var env = context.RequestServices.GetRequiredService<IWebHostEnvironment>();
                var exceptionHandlerFeature = context.Features.Get<IExceptionHandlerFeature>();
                var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

                logger.LogError(exceptionHandlerFeature?.Error, "Unhandled exception");

                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/problem+json";

                var problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "An error occurred processing your request.",
                    Detail = env.IsDevelopment() ? exceptionHandlerFeature?.Error?.ToString() : null,
                };

                await context.Response.WriteAsJsonAsync(problemDetails);
            });
        });

        // OpenAPI and Scalar UI (development only)
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }

        app.UseCors(corsPolicy);

        // Serve the React SPA from wwwroot (production / integrated mode)
        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.UseRouting();
        app.UseAuthorization();

        // Endpoint Mapping

        // Minimal API endpoints
        app.MapDiagnosticApi();

        // SignalR hub for real-time diagnostics notifications
        app.MapHub<DiagnosticHub>("/diagnosticHub");

        // MCP Streamable HTTP endpoint
        app.MapMcpEndpoint();

        // SPA fallback
        // any non-API route serves index.html for client-side routing
        app.MapFallbackToFile("index.html");

        app.Run();
    }

    /// <summary>
    /// Runs the MCP server in stdio-only mode (for Claude Desktop, Continue, etc.).
    /// Only Tier 1 tools (list_dumps, open_dump, list_sessions, close_session)
    /// are registered at startup. Tier 2 tools are registered dynamically when
    /// a dump is opened.
    /// </summary>
    private static async Task RunStdioMcpServer(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        // Load configuration
        builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

        // MCP configuration
        builder.Services.Configure<McpConfiguration>(builder.Configuration.GetSection("Mcp"));

        // General configuration (for DumpsFolder, etc.)
        builder.Services.Configure<GeneralConfiguration>(builder.Configuration.GetSection("General"));

        // Core services needed by MCP tools
        builder.Services.AddSingleton<QueriesService>();
        builder.Services.AddSingleton<InvestigationState>();
        builder.Services.AddSingleton<DebuggingSessionService>();
        builder.Services.AddSingleton<McpToolRegistry>();
        builder.Services.AddSingleton<McpSessionManager>();
        builder.Services.AddSingleton<McpInsightsGenerator>();
        builder.Services.AddSingleton<McpSessionTools>();
        builder.Services.AddSingleton<McpQueryTools>();
        builder.Services.AddSingleton<McpInspectionTools>();

        // Add MCP server with stdio transport
        builder.Services.AddMcpStdioServer();

        var host = builder.Build();
        await host.RunAsync();
    }
}
