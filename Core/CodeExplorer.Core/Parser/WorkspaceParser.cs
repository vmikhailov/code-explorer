using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser;

public class WorkspaceParser
{
    private readonly ParsingContext _ctx;
    private readonly string _absoluteWorkspacePath;
    private readonly GitIgnoreMatcher _gitignore;


    public WorkspaceParser(ParsingContext ctx)
    {
        _ctx = ctx;
        _absoluteWorkspacePath = ctx.AbsoluteWorkspacePath;
        _gitignore = new GitIgnoreMatcher(_absoluteWorkspacePath);
    }

    public async Task ParseAsync()
    {
        // 0. Get or create Workspace ID from database (auto-incremented)
        var wsId = await _ctx.DbClient.GetOrCreateWorkspaceIdAsync(_ctx.HostWorkspacePath);
        _ctx.WorkspaceId = wsId;
        _ctx.CancellationToken.ThrowIfCancellationRequested();

        await _ctx.DbClient.SaveEmptyWorkspaceNodeAsync(wsId, _ctx.HostWorkspacePath);
        _ctx.CancellationToken.ThrowIfCancellationRequested();

        var folderName = Path.GetFileName(_ctx.HostWorkspacePath);
        if (string.IsNullOrEmpty(folderName)) folderName = _ctx.HostWorkspacePath;

        var hostPath = PathTools.NormalizeToHostPath(_ctx.HostWorkspacePath);
        var workspaceNode = new WorkspaceNode(wsId, folderName, hostPath);

        // Create workspace-level FilesStructure to group physical workspace topology (like Git settings and top-level folders)
        var filesNodeId = $"{wsId}:files_structure";
        var workspaceFilesStructure = new FilesStructureNode(filesNodeId, "FilesStructure", hostPath);
        workspaceNode.Children.Add(workspaceFilesStructure);

        // Create workspace-level ProjectsStructure to group logical projects
        var projectsNodeId = $"{wsId}:projects_structure";
        var workspaceProjectsStructure = new ProjectsStructureNode(projectsNodeId, "ProjectsStructure", hostPath);
        workspaceNode.Children.Add(workspaceProjectsStructure);
        _ctx.ProjectsStructure = workspaceProjectsStructure;

        // Create workspace-level SyntaxStructure
        var syntaxNodeId = $"{wsId}:syntax_structure";
        var workspaceSyntaxStructure = new SyntaxStructureNode(syntaxNodeId, "SyntaxStructure", hostPath);
        workspaceNode.Children.Add(workspaceSyntaxStructure);
        _ctx.SyntaxStructure = workspaceSyntaxStructure;

        // Create workspace-level SemanticStructure
        var semanticNodeId = $"{wsId}:semantic_structure";
        var workspaceSemanticStructure = new SemanticStructureNode(semanticNodeId, "SemanticStructure", hostPath);
        workspaceNode.Children.Add(workspaceSemanticStructure);
        _ctx.SemanticStructure = workspaceSemanticStructure;

        // Parse and add Git settings if the workspace has a .git folder
        var gitSettingsNode = GitSettingsParser.Parse(wsId, _absoluteWorkspacePath);

        if (gitSettingsNode != null)
        {
            workspaceFilesStructure.Children.Add(gitSettingsNode);
        }

        // 2. Recursively scan, discovering projects inline!
        await ScanDirectoryAsync(_absoluteWorkspacePath, workspaceNode);

        // 2.5 Run syntax enrichment pass across all projects
        _ctx.Log(
            $"[WorkspaceParser] Starting syntax enrichment pass for {_ctx.ProjectsToEnrich.Count} projects...");

        foreach (var (projProcessor, projNode) in _ctx.ProjectsToEnrich)
        {
            _ctx.CancellationToken.ThrowIfCancellationRequested();
            await projProcessor.EnrichAsync(projNode);
        }
        
        // 3. Upload the entire Workspace Node tree using OntologyUploader
        _ctx.CancellationToken.ThrowIfCancellationRequested();
        await OntologyUploader.UploadNodeTreeAsync(workspaceNode, null, _ctx);

        // 4. Perform late binding
        _ctx.CancellationToken.ThrowIfCancellationRequested();
        await PerformLateBindingAsync(workspaceNode);
    }

    private async Task ScanDirectoryAsync(string currentDir, IOntologyNode parentNode)
    {
        _ctx.CancellationToken.ThrowIfCancellationRequested();
        var relativeDir = Path.GetRelativePath(_absoluteWorkspacePath, currentDir).Replace('\\', '/');
        if (relativeDir == ".") relativeDir = "";

        // 1. Check GitIgnore exclusions first
        if (!string.IsNullOrEmpty(relativeDir) && _gitignore.IsIgnored(relativeDir, true))
        {
            _ctx.Log($"[WorkspaceParser] GitIgnore: Ignoring directory '{relativeDir}'");
            return;
        }

        var dirName = Path.GetFileName(currentDir);
        if (string.IsNullOrEmpty(dirName)) dirName = currentDir;
        var dirNameLower = dirName.ToLowerInvariant();

        // 2. Generic default exclusions
        var genericExclusions = new HashSet<string>
        {
            ".git",
            ".github",
            ".vscode",
            ".idea",
            ".vs",
            ".go",
            "node_modules",
            "bin",
            "obj",
            "packages",
            "dist",
            "build",
            "scratch",
            "demo"
        };

        if (genericExclusions.Contains(dirNameLower))
        {
            _ctx.Log($"[WorkspaceParser] Generic: Skipping VCS/IDE/Build folder '{relativeDir}'");
            return;
        }

        // 3. Scan folder for project signatures to detect projects dynamically
        if (string.IsNullOrEmpty(relativeDir))
        {
            // Root workspace directory
            var files = Directory.GetFiles(currentDir);
            var isProject = WorkspaceIndexer._projectParsers.Any(p => p.IsProjectDirectory(currentDir, files));
            if (isProject)
            {
                var filesStructure = parentNode.Children.OfType<FilesStructureNode>().FirstOrDefault();
                var projectNode = await ProjectProcessor.DetectAndParseAsync(_ctx, currentDir, filesStructure ?? parentNode);
                if (projectNode != null)
                {
                    _ctx.ProjectsStructure?.Children.Add(projectNode);
                }
                return;
            }
        }
        else
        {
            // Subdirectories
            var absoluteFolderPath = Path.GetFullPath(currentDir).Replace('\\', '/');
            var folderId = $"{_ctx.WorkspaceId}:folder:{absoluteFolderPath}";
            var folderNode = new FolderNode(folderId, dirName, absoluteFolderPath);

            if (parentNode is WorkspaceNode wsNode)
            {
                var filesStructure = wsNode.Children.OfType<FilesStructureNode>().FirstOrDefault();
                if (filesStructure != null)
                {
                    filesStructure.Children.Add(folderNode);
                }
                else
                {
                    wsNode.Children.Add(folderNode);
                }
            }
            else
            {
                parentNode.Children.Add(folderNode);
            }

            var files = Directory.GetFiles(currentDir);
            var isProject = WorkspaceIndexer._projectParsers.Any(p => p.IsProjectDirectory(currentDir, files));
            if (isProject)
            {
                var projectNode = await ProjectProcessor.DetectAndParseAsync(_ctx, currentDir, folderNode);
                if (projectNode != null)
                {
                    _ctx.ProjectsStructure?.Children.Add(projectNode);
                }
                return; // Delegate subdirectory recursion inside the project to ProjectProcessor
            }

            parentNode = folderNode; // For recursing normal subdirectories
        }

        // Recurse into subdirectories
        foreach (var subDir in Directory.GetDirectories(currentDir))
        {
            await ScanDirectoryAsync(subDir, parentNode);
        }
    }

    private async Task PerformLateBindingAsync(IOntologyNode rootNode)
    {
        var entryPoints = new List<EntryPointNode>();
        var endpoints = new List<EndpointNode>();
        var externalServices = new List<ExternalServiceNode>();

        CollectPublicSymbols(rootNode, entryPoints, endpoints, externalServices);

        _ctx.Log(
            $"[LateBinding] Found {entryPoints.Count} EntryPoints, {endpoints.Count} Endpoints, and {externalServices.Count} ExternalServices in the workspace.");

        var lateBoundRels = new List<Relationship>();

        foreach (var extService in externalServices)
        {
            foreach (var entryPoint in entryPoints)
            {
                if (IsMatch(extService, entryPoint))
                {
                    _ctx.Log(
                        $"[LateBinding] Binding ExternalService '{extService.Id}' to EntryPoint '{entryPoint.Id}'");
                    var rel = Relationship.FromRelationship(new CallsRelationship(extService.Id, entryPoint.Id));
                    lateBoundRels.Add(rel);
                }
            }

            foreach (var endpoint in endpoints)
            {
                if (IsMatch(extService, endpoint))
                {
                    _ctx.Log(
                        $"[LateBinding] Binding ExternalService '{extService.Id}' to Endpoint '{endpoint.Id}'");
                    var rel = Relationship.FromRelationship(new CallsEndpointRelationship(extService.Id, endpoint.Id));
                    lateBoundRels.Add(rel);
                }
            }
        }

        if (lateBoundRels.Count > 0)
        {
            _ctx.Log($"[LateBinding] Enqueuing {lateBoundRels.Count} late-bound relationships...");
            await _ctx.EnqueueUploadRelationshipsAsync(lateBoundRels);
            _ctx.AddRelsCount(lateBoundRels.Count);
        }
    }

    private void CollectPublicSymbols(
        IOntologyNode node,
        List<EntryPointNode> entryPoints,
        List<EndpointNode> endpoints,
        List<ExternalServiceNode> externalServices)
    {
        if (node is EntryPointNode ep)
        {
            entryPoints.Add(ep);
        }
        else if (node is EndpointNode endp)
        {
            endpoints.Add(endp);
        }
        else if (node is ExternalServiceNode es)
        {
            externalServices.Add(es);
        }

        foreach (var child in node.Children)
        {
            CollectPublicSymbols(child, entryPoints, endpoints, externalServices);
        }
    }

    private bool IsMatch(ExternalServiceNode extService, EntryPointNode entryPoint)
    {
        var servicePathNorm = NormalizePath(extService.Path);
        var serviceDomainNorm = NormalizePath(extService.DomainOrService);
        var entryNorm = NormalizePath(entryPoint.Name);

        if (string.IsNullOrEmpty(entryNorm))
        {
            return false;
        }

        if (string.Equals(servicePathNorm, entryNorm, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(serviceDomainNorm, entryNorm, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private bool IsMatch(ExternalServiceNode extService, EndpointNode endpoint)
    {
        var servicePathNorm = NormalizePath(extService.Path);
        var serviceDomainNorm = NormalizePath(extService.DomainOrService);
        var routeNorm = NormalizePath(endpoint.RouteTemplate);

        if (string.IsNullOrEmpty(routeNorm))
        {
            return false;
        }

        if (string.Equals(servicePathNorm, routeNorm, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(servicePathNorm) &&
            (servicePathNorm.EndsWith("/" + routeNorm, StringComparison.OrdinalIgnoreCase) ||
             servicePathNorm.EndsWith(routeNorm, StringComparison.OrdinalIgnoreCase) ||
             routeNorm.EndsWith("/" + servicePathNorm, StringComparison.OrdinalIgnoreCase) ||
             routeNorm.EndsWith(servicePathNorm, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (string.Equals(serviceDomainNorm, routeNorm, StringComparison.OrdinalIgnoreCase))
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
