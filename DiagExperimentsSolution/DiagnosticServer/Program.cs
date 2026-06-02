using DiagnosticInvestigations;
using DiagnosticInvestigations.Configurations;

using DiagnosticServer.Extensions;
using DiagnosticServer.Hubs;
using DiagnosticServer.Services;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

using Scalar.AspNetCore;

namespace DiagnosticServer;

public class Program
{
    public static void Main(string[] args)
    {
        const string corsPolicy = "CorsPolicy";

        var builder = WebApplication.CreateBuilder(args);

        // Kestrel: allow large request bodies for dump file uploads
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = 2_147_483_648; // 2 GB
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

        // JSON: use camelCase property names (JavaScript convention)
        builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        });

        // Application Services
        builder.Services.AddSingleton<DebuggingSessionService>();
        builder.Services.AddHostedService<DebuggingSessionService>(
            provider => provider.GetRequiredService<DebuggingSessionService>());
        builder.Services.AddSingleton<QueriesService>();
        builder.Services.AddSingleton<InvestigationState>();

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

        // SPA fallback
        // any non-API route serves index.html for client-side routing
        app.MapFallbackToFile("index.html");

        app.Run();
    }
}
