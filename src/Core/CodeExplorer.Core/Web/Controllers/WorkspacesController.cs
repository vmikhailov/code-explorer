using CodeExplorer.Core.Mcp;
using CodeExplorer.Core.Mcp.Models;
using CodeExplorer.Core.Parser;
using Microsoft.AspNetCore.Mvc;

namespace CodeExplorer.Core.Web.Controllers;

[ApiController]
[Route("api/workspaces")]
public class WorkspacesController(CodeExplorerRepository repository, IndexingTaskManager taskManager)
    : ControllerBase
{
    [HttpPost("index")]
    [HttpPost("index/start")]
    public IActionResult StartIndexAsync([FromBody] WorkspaceIndexRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Dir))
            {
                return BadRequest(new { error = "Directory 'dir' is required." });
            }

            var taskId = taskManager.StartIndex(request.Dir, request.Clear, out var message);
            if (taskId == null)
            {
                return Conflict(new { error = message });
            }

            return Accepted(new { taskId, message, status = taskManager.GetStatus(taskId) });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("index/stop")]
    public IActionResult StopIndex([FromQuery] string? taskId)
    {
        try
        {
            var success = taskManager.StopIndex(taskId, out var message);
            if (!success)
            {
                return BadRequest(new { error = message });
            }

            return Ok(new { message, status = taskManager.GetStatus(taskId) });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("index/status")]
    public IActionResult GetIndexStatus([FromQuery] string? taskId)
    {
        try
        {
            var status = taskManager.GetStatus(taskId);
            if (status == null)
            {
                return NotFound(new { error = taskId == null ? "No active task found." : $"Task with ID '{taskId}' not found." });
            }
            return Ok(status);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("index/status/all")]
    public IActionResult GetIndexStatusAll()
    {
        try
        {
            return Ok(taskManager.GetAllStatuses());
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet]
    [HttpGet("all")]
    public async Task<IActionResult> GetAllWorkspacesAsync()
    {
        try
        {
            var resultJson = await repository.GetAllWorkspacesAsync();
            return Content(resultJson, "application/json");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("content")]
    public async Task<IActionResult> GetContentAsync([FromQuery] string? workspacePath, [FromQuery] string? type)
    {
        try
        {
            var resultJson = await repository.GetWorkspaceContentAsync(workspacePath, type);
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

            var resultJson = await repository.ExecuteRawQueryAsync(request.Query, request.Parameters);
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
            var resultJson = await repository.GetTaxonomyAsync(path);
            return Content(resultJson, "application/json");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("architecture-map")]
    public async Task<IActionResult> GetArchitectureMapAsync([FromQuery] string? projectName = null, [FromQuery] string? workspacePath = null)
    {
        try
        {
            var path = workspacePath ?? Directory.GetCurrentDirectory();
            var resultJson = await repository.GetArchitectureMapAsync(projectName, path);
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

            if (!Directory.Exists(dir))
            {
                return NotFound(new { 
                    error = $"Directory does not exist.",
                    inputDir = dir
                });
            }

            var files = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                .Take(100)
                .Select(f => Path.GetRelativePath(dir, f).Replace('\\', '/'))
                .ToList();

            return Ok(new
            {
                inputDir = dir,
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
