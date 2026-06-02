using CodeExplorer.Database;
using CodeExplorer.Mcp.Models;
using CodeExplorer.Parser;
using Microsoft.AspNetCore.Mvc;

namespace CodeExplorer.Web.Controllers;

[ApiController]
[Route("api/workspaces")]
public class WorkspacesController : ControllerBase
{
    private readonly MemgraphClient _client;

    public WorkspacesController(MemgraphClient client)
    {
        _client = client;
    }

    [HttpPost("index")]
    public async Task<IActionResult> IndexAsync([FromBody] WorkspaceIndexRequest request)
    {
        try
        {
            var indexer = new WorkspaceIndexerService(_client);
            var (nodesCount, relsCount, nodesByKind) = await indexer.IndexWorkspaceAsync(request.Dir, request.Clear);
            return Ok(new
            {
                message = "Workspace indexed successfully.",
                directory = request.Dir,
                nodesCount,
                relationshipsCount = relsCount,
                nodesByKind
            });
        }
        catch (DirectoryNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }

    [HttpGet("content")]
    public async Task<IActionResult> GetContentAsync([FromQuery] string? workspacePath, [FromQuery] string? type)
    {
        try
        {
            string query;
            var parameters = new Dictionary<string, object?>();

            if (!string.IsNullOrEmpty(workspacePath))
            {
                var absolutePath = Path.GetFullPath(workspacePath).Replace('\\', '/');
                parameters["workspacePath"] = absolutePath;
                parameters["type"] = string.IsNullOrEmpty(type) ? null : type;

                query = @"
                    MATCH (r:Root {path: $workspacePath})-[:CONTAINS*0..]->(n)
                    WHERE $type IS NULL OR $type = '' OR any(lbl IN labels(n) WHERE lbl = $type)
                    RETURN n LIMIT 1000";
            }
            else
            {
                parameters["type"] = string.IsNullOrEmpty(type) ? null : type;

                query = @"
                    MATCH (n)
                    WHERE $type IS NULL OR $type = '' OR any(lbl IN labels(n) WHERE lbl = $type)
                    RETURN n LIMIT 1000";
            }

            var resultJson = await _client.ExecuteQueryAsync(query, parameters);
            return Content(resultJson, "application/json");
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }
}
