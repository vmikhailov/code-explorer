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
            var resultJson = await repository.GetArchitectureMapAsync(projectName, workspacePath);
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

    [HttpDelete("all")]
    [HttpDelete("database")]
    [HttpPost("clear-all")]
    public async Task<IActionResult> ClearAllAsync()
    {
        try
        {
            await repository.ClearAllAsync();
            return Ok(new { message = "Database cleared successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpDelete("workspace/{idOrPath}")]
    [HttpDelete("{idOrPath}")]
    public async Task<IActionResult> DeleteWorkspaceAsync([FromRoute] string idOrPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(idOrPath))
            {
                return BadRequest(new { error = "Workspace ID, name, or path is required." });
            }

            var success = await repository.ClearWorkspaceAsync(idOrPath);
            if (!success)
            {
                return NotFound(new { error = $"Workspace '{idOrPath}' not found." });
            }

            return Ok(new { message = $"Workspace '{idOrPath}' cleared successfully.", workspace = idOrPath });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteWorkspaceByQueryAsync(
        [FromQuery] string? idOrPath,
        [FromQuery] string? workspaceId,
        [FromQuery] string? workspacePath,
        [FromQuery] bool? all)
    {
        try
        {
            if (all == true)
            {
                await repository.ClearAllAsync();
                return Ok(new { message = "Database cleared successfully." });
            }

            var target = idOrPath ?? workspaceId ?? workspacePath;
            if (string.IsNullOrWhiteSpace(target))
            {
                return BadRequest(new { error = "Please provide 'idOrPath', 'workspaceId', 'workspacePath', or '?all=true'." });
            }

            var success = await repository.ClearWorkspaceAsync(target);
            if (!success)
            {
                return NotFound(new { error = $"Workspace '{target}' not found." });
            }

            return Ok(new { message = $"Workspace '{target}' cleared successfully.", workspace = target });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("clear")]
    public async Task<IActionResult> ClearAsync([FromBody] ClearWorkspacesRequest? request, [FromQuery] bool? all)
    {
        try
        {
            if (all == true || request?.All == true)
            {
                await repository.ClearAllAsync();
                return Ok(new { message = "Database cleared successfully." });
            }

            var targets = new List<string>();
            if (!string.IsNullOrWhiteSpace(request?.WorkspaceId)) targets.Add(request.WorkspaceId);
            if (!string.IsNullOrWhiteSpace(request?.WorkspacePath)) targets.Add(request.WorkspacePath);
            if (request?.Workspaces != null) targets.AddRange(request.Workspaces.Where(w => !string.IsNullOrWhiteSpace(w)));

            targets = targets.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            if (targets.Count == 0)
            {
                return BadRequest(new { error = "Specify 'all: true', 'workspaceId', 'workspacePath', or 'workspaces' list in the request." });
            }

            var (cleared, notFound) = await repository.ClearWorkspacesAsync(targets);

            return Ok(new
            {
                message = $"Processed {targets.Count} workspace(s): {cleared.Count} cleared, {notFound.Count} not found.",
                cleared,
                notFound
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

public class ClearWorkspacesRequest
{
    public bool All { get; set; }
    public string? WorkspaceId { get; set; }
    public string? WorkspacePath { get; set; }
    public List<string>? Workspaces { get; set; }
}
