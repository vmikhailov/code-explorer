using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Mcp;
using CodeExplorer.Core.Mcp.Models;
using CodeExplorer.Core.Parser;
using Microsoft.AspNetCore.Mvc;

namespace CodeExplorer.Core.Web.Controllers;

[ApiController]
[Route("api/workspaces")]
public class WorkspacesController : ControllerBase
{
    private readonly CodeExplorerRepository _repository;
    private readonly WorkspaceIndexer _indexer;

    public WorkspacesController(CodeExplorerRepository repository, WorkspaceIndexer indexer)
    {
        _repository = repository;
        _indexer = indexer;
    }

    [HttpPost("index")]
    public async Task<IActionResult> IndexAsync([FromBody] WorkspaceIndexRequest request)
    {
        try
        {
            var (nodesCount, relsCount, nodesByKind) = await _indexer.IndexAsync(request.Dir, request.Dir, request.Clear);
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
            var resultJson = await _repository.GetWorkspaceContentAsync(workspacePath, type);
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

            var resultJson = await _repository.ExecuteRawQueryAsync(request.Query, request.Parameters);
            return Content(resultJson, "application/json");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("taxonomy")]
    public async Task<IActionResult> GetTaxonomyAsync([FromQuery] string? workspacePath = null)
    {
        try
        {
            var path = workspacePath ?? Directory.GetCurrentDirectory();
            var resultJson = await _repository.GetTaxonomyAsync(path);
            return Content(resultJson, "application/json");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("node-definition")]
    public IActionResult GetNodeDefinition([FromQuery] string kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return BadRequest(new { error = "Node kind parameter 'kind' is required." });
        }

        var definition = OntologyRegistry.GetNodeDefinition(kind);
        if (definition == "Invalid node kind.")
        {
            return BadRequest(new { error = definition });
        }
        if (definition.StartsWith("Unknown node kind:"))
        {
            return NotFound(new { error = definition });
        }

        return Ok(new { definition });
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

            var resolvedPath = PathTools.TranslateHostPathToContainerPath(dir);
            if (!Directory.Exists(resolvedPath))
            {
                return NotFound(new { 
                    error = $"Directory does not exist.",
                    inputDir = dir,
                    resolvedDir = resolvedPath,
                    inContainer = PathTools.InContainer,
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
