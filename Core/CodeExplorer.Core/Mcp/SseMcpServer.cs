using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CodeExplorer.Database;

namespace CodeExplorer.Mcp;

public class SseMcpServer(MemgraphClient dbClient, int port)
{
    public async Task StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.AddFilter("Microsoft", Microsoft.Extensions.Logging.LogLevel.Warning);
        builder.Logging.AddFilter("System", Microsoft.Extensions.Logging.LogLevel.Warning);
        
        builder.Services.AddCors(options => options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
        
        // Explicitly register controllers assembly to ensure discovery in multi-project setup
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(CodeExplorer.Mcp.Controllers.McpController).Assembly);
            
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
            {
                Title = "CodeExplorer API (MCP & REST Management)",
                Version = "v1",
                Description = "Unified server hosting both the Model Context Protocol (MCP) SSE transport and REST management controllers."
            });
        });

        // Register dependencies in DI container
        builder.Services.AddSingleton(dbClient);
        builder.Services.AddSingleton<McpServer>();

        var app = builder.Build();
        
        app.UseCors();

        // Expose Swagger UI
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "CodeExplorer API v1");
            c.RoutePrefix = "swagger";
        });

        // Map API controllers
        app.MapControllers();

        // Redirect root to /swagger for developer convenience
        app.MapGet("/", HandleRootRedirect);

        Console.Error.WriteLine($"Starting Unified CodeExplorer Web Service (MCP + REST Management) on http://localhost:{port}...");
        Console.Error.WriteLine($"Swagger UI available at http://localhost:{port}/swagger");
        await app.RunAsync($"http://0.0.0.0:{port}");
    }

    private static Task HandleRootRedirect(HttpContext context)
    {
        context.Response.Redirect("/swagger");
        return Task.CompletedTask;
    }
}
