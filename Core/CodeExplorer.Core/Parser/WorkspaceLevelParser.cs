using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser;

public class WorkspaceLevelParser
{
    private readonly ParsingContext _ctx;
    private readonly string _absoluteWorkspacePath;
    private readonly GitIgnoreMatcher _gitignore;
    private readonly List<SyntaxTree> _workspaceSyntaxTrees = new();

    public WorkspaceLevelParser(ParsingContext ctx)
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

        // 1. Clear database sequentially at root to avoid contentions
        if (_ctx.Clear)
        {
            _ctx.Log($"[WorkspaceParser] Clearing previous root workspace data for '{_ctx.HostWorkspacePath}'...");
            await _ctx.DbClient.ClearWorkspaceAsync(_ctx.HostWorkspacePath);
        }

        // Save/reserve empty Workspace node first to define ID
        await _ctx.DbClient.SaveEmptyWorkspaceNodeAsync(wsId, _ctx.HostWorkspacePath);

        var folderName = Path.GetFileName(_ctx.HostWorkspacePath);
        if (string.IsNullOrEmpty(folderName)) folderName = _ctx.HostWorkspacePath;

        var hostPath = PathTools.NormalizeToHostPath(_ctx.HostWorkspacePath);
        var workspaceNode = new WorkspaceNode(
            wsId.ToString(),
            folderName,
            hostPath
        );

        // Parse and add Git settings if the workspace has a .git folder
        var gitSettingsNode = GitSettingsParser.Parse(wsId.ToString(), _absoluteWorkspacePath);
        if (gitSettingsNode != null)
        {
            workspaceNode.Children.Add(gitSettingsNode);
        }

        try
        {
            // 2. Recursively scan, discovering projects inline!
            await ScanDirectoryAsync(_absoluteWorkspacePath, workspaceNode);

            // Prune empty folder/project nodes to avoid adding empty projects/folders to the graph
            OntologyPruner.PruneEmptyFolders(workspaceNode);

            // 3. Perform late binding
            await PerformLateBindingAsync(workspaceNode);

            // 4. Upload the entire Workspace Node tree using OntologyUploader
            await OntologyUploader.UploadNodeTreeAsync(workspaceNode, null, _ctx);
        }
        finally
        {
            foreach (var st in _workspaceSyntaxTrees)
            {
                st.Dispose();
            }
            _workspaceSyntaxTrees.Clear();
        }
    }

    private async Task ScanDirectoryAsync(string currentDir, IOntologyNode parentNode)
    {
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
            ".git", ".github", ".vscode", ".idea", ".vs", ".go",
            "node_modules", "bin", "obj", "packages", "dist", "build" 
        };
        if (genericExclusions.Contains(dirNameLower))
        {
            _ctx.Log($"[WorkspaceParser] Generic: Skipping VCS/IDE/Build folder '{relativeDir}'");
            return;
        }

        // 3. Scan folder for project signatures to detect projects dynamically
        var processor = ProjectProcessorFactory.CreateProcessor(_ctx, currentDir, parentNode.Id);
        if (processor != null)
        {
            var projectNode = await processor.ProcessAsync();
            parentNode.Children.Add(projectNode);
            return;
        }

        // Register current directory in structural nodes
        var currentParentNode = parentNode;
        if (!string.IsNullOrEmpty(relativeDir))
        {
            var folderId = $"{_ctx.WorkspaceId}:workspacefolder:{relativeDir}";
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
        foreach (var file in Directory.GetFiles(currentDir))
        {
            var ext = Path.GetExtension(file).ToLower();
            var relativeFile = Path.GetRelativePath(_absoluteWorkspacePath, file).Replace('\\', '/');

            if (_gitignore.IsIgnored(relativeFile, false))
            {
                _ctx.Log($"[WorkspaceParser] GitIgnore: Ignoring file '{relativeFile}'");
                continue;
            }

            IFileParser? fileParser = null;
            lock (WorkspaceParser.FileParsers)
            {
                fileParser = WorkspaceParser.FileParsers.FirstOrDefault(p => p.CanParse(ext));
            }

            if (fileParser != null)
            {
                var syntaxTree = await fileParser.ParseAsync(file, currentParentNode.Id, _ctx.WorkspaceId, _ctx.AbsoluteWorkspacePath);
                if (syntaxTree != null)
                {
                    if (syntaxTree.FileNode != null)
                    {
                        currentParentNode.Children.Add(syntaxTree.FileNode);
                    }
                    _workspaceSyntaxTrees.Add(syntaxTree);

                    lock (_ctx.RawImports)
                    {
                        _ctx.RawImports.AddRange(syntaxTree.RawImports);
                    }
                    lock (_ctx.RawVariables)
                    {
                        _ctx.RawVariables.AddRange(syntaxTree.RawVariables);
                    }
                }
            }
        }
    }

    private async Task PerformLateBindingAsync(IOntologyNode rootNode)
    {
        var entryPoints = new List<EntryPointNode>();
        var externalServices = new List<ExternalServiceNode>();

        CollectPublicSymbols(rootNode, entryPoints, externalServices);

        _ctx.Log($"[LateBinding] Found {entryPoints.Count} EntryPoints and {externalServices.Count} ExternalServices in the workspace.");

        var lateBoundRels = new List<Relationship>();

        foreach (var extService in externalServices)
        {
            foreach (var entryPoint in entryPoints)
            {
                if (IsMatch(extService, entryPoint))
                {
                    _ctx.Log($"[LateBinding] Binding ExternalService '{extService.Id}' to EntryPoint '{entryPoint.Id}'");
                    var rel = Relationship.FromRelationship(new CallsRelationship(extService.Id, entryPoint.Id));
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
