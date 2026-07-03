using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using DiagnosticInvestigations;
using DiagnosticInvestigations.Configurations;
using DiagnosticModels.Converters;
using DiagnosticServer.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using ModelContextProtocol.Server;

namespace DiagnosticServer.Mcp;

/// <summary>
/// Extension methods for registering MCP services in the DI container
/// and configuring transports. All tools (Tier 1 + Tier 2) are registered
/// at startup via <c>WithToolsFromAssembly</c>. Tier 2 tools check for an
/// active session and return helpful guidance when none is open.
/// </summary>
public static class McpExtensions
{
    /// <summary>
    /// Creates a JsonSerializerOptions instance safe for use with the MCP server.
    /// Includes custom ClrMD converters and an explicit TypeInfoResolver to
    /// satisfy the MCP SDK's read-only serialization requirements.
    /// </summary>
    private static JsonSerializerOptions CreateMcpSerializerOptions()
    {
        var options = SetupConverters.CreateOptions();
        // MCP SDK marks options as read-only; explicit resolver required
        options.TypeInfoResolver ??= new DefaultJsonTypeInfoResolver();
        return options;
    }

    /// <summary>
    /// Registers all MCP-related services: configuration, session manager,
    /// tool registry, insights generator, and the tool classes.
    /// </summary>
    public static IServiceCollection AddMcpServices(this IServiceCollection services, IConfiguration configuration)
    {
        // MCP configuration
        services.Configure<McpConfiguration>(configuration.GetSection("Mcp"));

        // Core MCP services
        services.AddSingleton<McpToolRegistry>();
        services.AddSingleton<McpSessionManager>();
        services.AddSingleton<McpInsightsGenerator>();

        // Tool classes (Tier 1 + Tier 2) — all registered at startup
        services.AddSingleton<McpSessionTools>();
        services.AddSingleton<McpQueryTools>();
        services.AddSingleton<McpInspectionTools>();

        return services;
    }

    /// <summary>
    /// Adds MCP server with stdio transport (for --stdio mode).
    /// All tools are discovered and registered from the assembly.
    /// Custom JSON converters for ClrMD types are applied to avoid
    /// serialization errors with non-serializable types (e.g., ReadOnlySpan).
    /// </summary>
    public static IServiceCollection AddMcpStdioServer(this IServiceCollection services)
    {
        services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly(
                serializerOptions: CreateMcpSerializerOptions());

        return services;
    }

    /// <summary>
    /// Adds MCP server with Streamable HTTP transport (for normal web mode).
    /// All tools are discovered and registered from the assembly.
    /// Custom JSON converters for ClrMD types are applied to avoid
    /// serialization errors with non-serializable types (e.g., ReadOnlySpan).
    /// </summary>
    public static IServiceCollection AddMcpHttpServer(this IServiceCollection services)
    {
        services
            .AddMcpServer()
            .WithHttpTransport()
            .WithToolsFromAssembly(
                serializerOptions: CreateMcpSerializerOptions());

        return services;
    }

    /// <summary>
    /// Maps the MCP HTTP endpoint on the application.
    /// </summary>
    public static WebApplication MapMcpEndpoint(this WebApplication app)
    {
        var config = app.Services.GetRequiredService<IOptions<McpConfiguration>>().Value;
        app.MapMcp(config.HttpEndpoint);
        return app;
    }
}

