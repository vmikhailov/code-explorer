using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CodeExplorer.Mcp.Controllers;

[ApiController]
[Route("")]
public class McpController : ControllerBase
{
    private readonly McpServer _mcpProcessor;
    private static readonly ConcurrentDictionary<string, HttpContext> _sessions = new();

    public McpController(McpServer mcpProcessor)
    {
        _mcpProcessor = mcpProcessor;
    }

    [HttpGet("sse")]
    public async Task GetSseAsync()
    {
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        var sessionId = Guid.NewGuid().ToString();
        _sessions[sessionId] = HttpContext;

        // Immediately send the client-specific message target URL
        await Response.WriteAsync($"event: endpoint\r\ndata: /message?sessionId={sessionId}\r\n\r\n");
        await Response.Body.FlushAsync();

        var clientDisconnected = HttpContext.RequestAborted;
        try
        {
            while (!clientDisconnected.IsCancellationRequested)
            {
                await Task.Delay(1000, clientDisconnected); // Keep-alive loop
            }
        }
        catch (OperationCanceledException)
        {
            // Normal client disconnect
        }
        finally
        {
            _sessions.TryRemove(sessionId, out _);
        }
    }

    [HttpPost("message")]
    public async Task<IActionResult> PostMessageAsync([FromQuery] string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            return BadRequest("Missing or invalid 'sessionId' query parameter.");
        }

        if (!_sessions.TryGetValue(sessionId, out var sseContext))
        {
            return BadRequest("Session not found or expired.");
        }

        using var reader = new StreamReader(Request.Body);
        var jsonLine = await reader.ReadToEndAsync();

        // Process payload using our agnostic core processor
        var responseJson = await _mcpProcessor.ProcessRequestAsync(jsonLine);

        if (!string.IsNullOrEmpty(responseJson))
        {
            // Push response back to LLM client over the SSE stream
            await sseContext.Response.WriteAsync($"event: message\r\ndata: {responseJson}\r\n\r\n");
            await sseContext.Response.Body.FlushAsync();
        }

        return Accepted();
    }
}
