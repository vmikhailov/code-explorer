using CodeExplorer.Common;
using CodeExplorer.Database;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CodeExplorer.Parser;

public class WorkspaceLevelParser
{
    private readonly ParsingContext _ctx;
    private readonly string _absoluteWorkspacePath;
    private readonly string _workspaceNodeId;
    private readonly GitIgnoreMatcher _gitignore;

    public WorkspaceLevelParser(ParsingContext ctx)
    {
        _ctx = ctx;
        _absoluteWorkspacePath = ctx.AbsoluteWorkspacePath;
        _workspaceNodeId = $"workspace:{_absoluteWorkspacePath}";
        _gitignore = new GitIgnoreMatcher(_absoluteWorkspacePath);
    }

    public async Task ParseAsync()
    {
        // 1. Clear database sequentially at root to avoid contentions
        if (_ctx.Clear)
        {
            await Console.Error.WriteLineAsync($"[WorkspaceParser] Clearing previous root workspace data for '{_absoluteWorkspacePath}'...");
            await _ctx.DbClient.ClearWorkspaceAsync(_absoluteWorkspacePath);
        }

        var folderName = Path.GetFileName(_absoluteWorkspacePath);
        if (string.IsNullOrEmpty(folderName)) folderName = _absoluteWorkspacePath;

        var workspaceNode = new WorkspaceNode(
            _workspaceNodeId,
            folderName,
            _absoluteWorkspacePath
        );

        // 2. Recursively scan, discovering projects inline!
        await ScanDirectoryAsync(_absoluteWorkspacePath, workspaceNode);

        // 3. Perform late binding
        await PerformLateBindingAsync(workspaceNode);

        // 4. Upload the entire Workspace Node tree using OntologyUploader
        await OntologyUploader.UploadNodeTreeAsync(workspaceNode, null, _ctx);
    }

    private async Task ScanDirectoryAsync(string currentDir, IOntologyNode parentNode)
    {
        var relativeDir = Path.GetRelativePath(_absoluteWorkspacePath, currentDir).Replace('\\', '/');
        if (relativeDir == ".") relativeDir = "";

        // 1. Check GitIgnore exclusions first
        if (!string.IsNullOrEmpty(relativeDir) && _gitignore.IsIgnored(relativeDir, true))
        {
            await Console.Error.WriteLineAsync($"[WorkspaceParser] GitIgnore: Ignoring directory '{relativeDir}'");
            return;
        }

        var dirName = Path.GetFileName(currentDir);
        if (string.IsNullOrEmpty(dirName)) dirName = currentDir;
        var dirNameLower = dirName.ToLowerInvariant();

        // 2. Generic default exclusions
        var genericExclusions = new HashSet<string> 
        { 
            ".git", ".github", ".vscode", ".idea", ".vs", ".go",
            "node_modules", "bin", "obj", "packages", "dist", "build" 
        };
        if (genericExclusions.Contains(dirNameLower))
        {
            await Console.Error.WriteLineAsync($"[WorkspaceParser] Generic: Skipping VCS/IDE/Build folder '{relativeDir}'");
            return;
        }

        // 3. Scan folder for project signatures to detect projects dynamically
        var filesInDir = Directory.GetFiles(currentDir);
        IProjectParser? matchedParser = null;
        lock (WorkspaceParser.ProjectParsers)
        {
            matchedParser = WorkspaceParser.ProjectParsers.FirstOrDefault(p => p.IsProjectDirectory(currentDir, filesInDir));
        }

        if (matchedParser != null)
        {
            var projectParser = new ProjectLevelParser(_ctx, currentDir, parentNode.Id, matchedParser);
            var projectNode = await projectParser.ParseProjectAsync();
            parentNode.Children.Add(projectNode);
            return;
        }

        // Register current directory in structural nodes
        var currentParentNode = parentNode;
        if (!string.IsNullOrEmpty(relativeDir))
        {
            var folderId = $"workspacefolder:{_absoluteWorkspacePath}:{relativeDir}";
            var folderNode = new WorkspaceFolderNode(folderId, dirName, relativeDir);
            parentNode.Children.Add(folderNode);
            currentParentNode = folderNode;
        }

        // Recurse into subdirectories
        foreach (var subDir in Directory.GetDirectories(currentDir))
        {
            await ScanDirectoryAsync(subDir, currentParentNode);
        }

        // Scan root level loose files
        foreach (var file in filesInDir)
        {
            var ext = Path.GetExtension(file).ToLower();
            var relativeFile = Path.GetRelativePath(_absoluteWorkspacePath, file).Replace('\\', '/');

            if (_gitignore.IsIgnored(relativeFile, false))
            {
                await Console.Error.WriteLineAsync($"[WorkspaceParser] GitIgnore: Ignoring file '{relativeFile}'");
                continue;
            }

            IFileParser? fileParser = null;
            lock (WorkspaceParser.FileParsers)
            {
                fileParser = WorkspaceParser.FileParsers.FirstOrDefault(p => p.CanParse(ext));
            }

            if (fileParser != null)
            {
                var fileNode = await fileParser.ParseAsync(file, currentParentNode.Id, _ctx);
                if (fileNode != null)
                {
                    currentParentNode.Children.Add(fileNode);
                }
            }
        }
    }

    private async Task PerformLateBindingAsync(IOntologyNode rootNode)
    {
        var entryPoints = new List<EntryPointNode>();
        var externalServices = new List<ExternalServiceNode>();

        CollectPublicSymbols(rootNode, entryPoints, externalServices);

        Console.Error.WriteLine($"[LateBinding] Found {entryPoints.Count} EntryPoints and {externalServices.Count} ExternalServices in the workspace.");

        var lateBoundRels = new List<Relationship>();

        foreach (var extService in externalServices)
        {
            foreach (var entryPoint in entryPoints)
            {
                if (IsMatch(extService, entryPoint))
                {
                    Console.Error.WriteLine($"[LateBinding] Binding ExternalService '{extService.Id}' to EntryPoint '{entryPoint.Id}'");
                    var rel = Relationship.FromRelationship(new CallsRelationship(extService.Id, entryPoint.Id));
                    lateBoundRels.Add(rel);
                }
            }
        }

        if (lateBoundRels.Count > 0)
        {
            Console.Error.WriteLine($"[LateBinding] Enqueuing {lateBoundRels.Count} late-bound relationships...");
            await _ctx.EnqueueUploadRelationshipsAsync(lateBoundRels);
            _ctx.AddRelsCount(lateBoundRels.Count);
        }
    }

    private void CollectPublicSymbols(IOntologyNode node, List<EntryPointNode> entryPoints, List<ExternalServiceNode> externalServices)
    {
        if (node is EntryPointNode ep)
        {
            entryPoints.Add(ep);
        }
        else if (node is ExternalServiceNode es)
        {
            externalServices.Add(es);
        }

        foreach (var child in node.Children)
        {
            CollectPublicSymbols(child, entryPoints, externalServices);
        }
    }

    private bool IsMatch(ExternalServiceNode extService, EntryPointNode entryPoint)
    {
        if (!string.Equals(extService.Protocol, entryPoint.Protocol, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var serviceNorm = NormalizePath(extService.DomainOrService);
        var routeNorm = NormalizePath(entryPoint.RouteOrTopic);

        if (string.IsNullOrEmpty(serviceNorm) || string.IsNullOrEmpty(routeNorm))
        {
            return false;
        }

        if (string.Equals(serviceNorm, routeNorm, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (serviceNorm.EndsWith("/" + routeNorm, StringComparison.OrdinalIgnoreCase) ||
            serviceNorm.EndsWith(routeNorm, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;
        var normalized = path.Replace('\\', '/').ToLowerInvariant();

        var protocolIdx = normalized.IndexOf("://");
        if (protocolIdx != -1)
        {
            normalized = normalized.Substring(protocolIdx + 3);
        }

        return normalized.Trim('/');
    }
}
