using System.Text.Json;
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
                var resolvedPath = Common.PathTools.TranslateHostPathToContainerPath(workspacePath);
                var absolutePath = Path.GetFullPath(resolvedPath).Replace('\\', '/');
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

    [HttpPost("query")]
    public async Task<IActionResult> ExecuteCustomQueryAsync([FromBody] CustomQueryRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return BadRequest(new { error = "Query is required." });
            }

            var resultJson = await _client.ExecuteQueryAsync(request.Query, request.Parameters);
            return Content(resultJson, "application/json");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }



    [HttpGet("taxonomy")]
    public async Task<IActionResult> GetTaxonomyAsync()
    {
        try
        {
            var query =
                "MATCH (n)-[r]->(m) WITH DISTINCT labels(n)[0] AS fromLabel, type(r) AS relType, labels(m)[0] " +
                "AS toLabel RETURN fromLabel, relType, toLabel";
            var resultJson = await _client.ExecuteQueryAsync(query);
            var parsedTriplets = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(resultJson) ?? [];

            var propQuery = "MATCH (n) UNWIND labels(n) AS label UNWIND keys(n) AS key RETURN DISTINCT label, key";
            var propJson = await _client.ExecuteQueryAsync(propQuery);
            var parsedProperties = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(propJson) ?? [];

            var taxonomy = Mcp.CodeExplorerRepository.BuildTaxonomy(parsedTriplets, parsedProperties);
            return Content(JsonSerializer.Serialize(new { taxonomy }), "application/json");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("test-files")]
    public IActionResult TestFiles([FromQuery] string dir)
    {
        try
        {
            if (string.IsNullOrEmpty(dir))
            {
                return BadRequest(new { error = "Directory parameter 'dir' is required." });
            }

            var resolvedPath = Common.PathTools.TranslateHostPathToContainerPath(dir);
            if (!Directory.Exists(resolvedPath))
            {
                return NotFound(new { 
                    error = $"Directory does not exist.",
                    inputDir = dir,
                    resolvedDir = resolvedPath,
                    inContainer = Common.PathTools.InContainer,
                    hostExists = Directory.Exists("/host")
                });
            }

            var files = Directory.EnumerateFiles(resolvedPath, "*", SearchOption.AllDirectories)
                .Take(100)
                .Select(f => Path.GetRelativePath(resolvedPath, f).Replace('\\', '/'))
                .ToList();

            return Ok(new
            {
                inputDir = dir,
                resolvedDir = resolvedPath,
                filesCount = files.Count,
                files
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

public class CustomQueryRequest
{
    public string Query { get; set; } = string.Empty;
    public Dictionary<string, object?>? Parameters { get; set; }
}
