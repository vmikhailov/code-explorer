using TreeSitter;

namespace CodeExplorer.Parser;

public class WorkspaceParser
{
    private static readonly List<ILanguageParser> Parsers = new();

    public static void Register(ILanguageParser parser)
    {
        lock (Parsers)
        {
            if (!Parsers.Any(p => p.GetType() == parser.GetType()))
            {
                Parsers.Add(parser);
            }
        }
    }

    // Instance-level state fields for the current indexing run
    private readonly string _absoluteWorkspacePath;
    private readonly Database.MemgraphClient _dbClient;
    private readonly bool _clear;
    private readonly string _workspaceNodeId;

    private readonly List<Database.Node> _structuralNodes = new();
    private readonly List<Database.Relationship> _structuralRelationships = new();
    private readonly Dictionary<string, List<string>> _projectFiles = new();
    private readonly Dictionary<string, (string Id, string Kind)> _visitedDirs = new();
    private readonly GitIgnoreMatcher _gitignore;

    private readonly Dictionary<(string Kind, string Name), string> _globalSymbols = new();
    private readonly List<Reference> _globalReferences = new();
    private readonly Dictionary<string, int> _nodesByKind = new(StringComparer.OrdinalIgnoreCase);

    private int _totalNodesCount;
    private int _totalRelsCount;

    public WorkspaceParser(string dirPath, Database.MemgraphClient dbClient, bool clear)
    {
        _absoluteWorkspacePath = Path.GetFullPath(dirPath).Replace('\\', '/');
        _dbClient = dbClient;
        _clear = clear;
        _gitignore = new GitIgnoreMatcher(_absoluteWorkspacePath);

        var folderName = Path.GetFileName(_absoluteWorkspacePath);
        if (string.IsNullOrEmpty(folderName)) folderName = _absoluteWorkspacePath;
        _workspaceNodeId = $"workspace:{_absoluteWorkspacePath}";

        _nodesByKind["Workspace"] = 1;
        _totalNodesCount = 1; // Workspace node
    }

    private void Scan(string currentDir, string currentParentId, HashSet<string> activeProjectTypes, bool insideProject)
    {
        var relativeDir = Path.GetRelativePath(_absoluteWorkspacePath, currentDir).Replace('\\', '/');
        if (relativeDir == ".") relativeDir = "";

        // 1. Check GitIgnore exclusions first
        if (!string.IsNullOrEmpty(relativeDir) && _gitignore.IsIgnored(relativeDir, true))
        {
            Console.Error.WriteLine($"[WorkspaceParser] GitIgnore: Ignoring directory '{relativeDir}'");
            return;
        }

        var dirName = Path.GetFileName(currentDir);
        if (string.IsNullOrEmpty(dirName))
        {
            dirName = currentDir;
        }
        var dirNameLower = dirName.ToLowerInvariant();

        // 2. Generic default exclusions
        var genericExclusions = new HashSet<string> { ".git", ".github", ".vscode", ".idea" };
        if (genericExclusions.Contains(dirNameLower))
        {
            Console.Error.WriteLine($"[WorkspaceParser] Generic: Skipping VCS/IDE folder '{relativeDir}'");
            return;
        }

        // 3. Scan current folder for project signatures by querying registered language parsers
        var filesInDir = Directory.GetFiles(currentDir);
        var newlyDetectedTypes = new HashSet<string>();
        bool isProject = false;
        string? projectType = null;

        if (!insideProject)
        {
            lock (Parsers)
            {
                foreach (var parser in Parsers)
                {
                    if (parser.IsProjectDirectory(currentDir, filesInDir))
                    {
                        newlyDetectedTypes.Add(parser.ProjectType);
                        isProject = true;
                        projectType = parser.ProjectType;
                        Console.Error.WriteLine($"[WorkspaceParser] Project: Detected {parser.ProjectType} project signature at '{relativeDir}'");
                    }
                }
            }
        }

        // Propagate whether we are inside a project to subdirectories
        bool currentInsideProject = insideProject || isProject;

        // 4. Add to active project types
        foreach (var type in newlyDetectedTypes)
        {
            activeProjectTypes.Add(type);
        }

        // 5. Check if current directory name should be excluded based on active project types and language exclusions
        bool shouldExclude = false;
        string? matchedExclusionFolder = null;
        string? matchedExclusionType = null;
        lock (Parsers)
        {
            foreach (var type in activeProjectTypes)
            {
                var parser = Parsers.FirstOrDefault(p => p.ProjectType == type);
                if (parser != null)
                {
                    foreach (var folder in parser.ExcludedFolders)
                    {
                        if (folder.Equals(dirNameLower, StringComparison.OrdinalIgnoreCase))
                        {
                            shouldExclude = true;
                            matchedExclusionFolder = folder;
                            matchedExclusionType = type;
                            break;
                        }
                    }
                }
                if (shouldExclude) break;
            }
        }

        if (shouldExclude)
        {
            Console.Error.WriteLine($"[WorkspaceParser] Exclusion: Skipping directory '{relativeDir}' (matches language exclusion '{matchedExclusionFolder}' for '{matchedExclusionType}' project type)");
            return;
        }

        // Register current directory in _visitedDirs and _structuralNodes
        string currentId;
        string currentKind;

        if (string.IsNullOrEmpty(relativeDir))
        {
            if (isProject)
            {
                currentId = $"project:{_absoluteWorkspacePath}:";
                currentKind = "Project";
                _structuralNodes.Add(new Database.Node(currentId, "Project", new Dictionary<string, object> 
                { 
                    ["name"] = dirName,
                    ["path"] = "",
                    ["project_type"] = projectType ?? "unknown"
                }));
                _structuralRelationships.Add(new Database.Relationship(_workspaceNodeId, currentId, "CONTAINS"));
                Console.Error.WriteLine($"[WorkspaceParser] Mapping root directory as Project Node: '{currentId}'");
            }
            else
            {
                currentId = _workspaceNodeId;
                currentKind = "Workspace";
            }
            _visitedDirs[relativeDir] = (currentId, currentKind);
        }
        else
        {
            if (isProject)
            {
                currentId = $"project:{_absoluteWorkspacePath}:{relativeDir}";
                currentKind = "Project";
                _structuralNodes.Add(new Database.Node(currentId, "Project", new Dictionary<string, object> 
                { 
                    ["name"] = dirName,
                    ["path"] = relativeDir,
                    ["project_type"] = projectType ?? "unknown"
                }));
                Console.Error.WriteLine($"[WorkspaceParser] Mapping directory '{relativeDir}' as Project Node");
            }
            else
            {
                if (insideProject)
                {
                    currentId = $"projectfolder:{_absoluteWorkspacePath}:{relativeDir}";
                    currentKind = "ProjectFolder";
                    _structuralNodes.Add(new Database.Node(currentId, "ProjectFolder", new Dictionary<string, object> 
                    { 
                        ["name"] = dirName,
                        ["path"] = relativeDir 
                    }));
                }
                else
                {
                    currentId = $"workspacefolder:{_absoluteWorkspacePath}:{relativeDir}";
                    currentKind = "WorkspaceFolder";
                    _structuralNodes.Add(new Database.Node(currentId, "WorkspaceFolder", new Dictionary<string, object> 
                    { 
                        ["name"] = dirName,
                        ["path"] = relativeDir 
                    }));
                }
            }

            _visitedDirs[relativeDir] = (currentId, currentKind);

            // Establish relationship from Parent Directory to Current Directory
            var parentPath = Path.GetDirectoryName(currentDir)!.Replace('\\', '/');
            var parentRelative = Path.GetRelativePath(_absoluteWorkspacePath, parentPath).Replace('\\', '/');
            if (parentRelative == ".") parentRelative = "";

            if (_visitedDirs.TryGetValue(parentRelative, out var parentInfo))
            {
                _structuralRelationships.Add(new Database.Relationship(parentInfo.Id, currentId, "CONTAINS"));
            }
        }

        string nextParentId = isProject ? currentId : currentParentId;

        // Add matching source files in this folder
        foreach (var file in filesInDir)
        {
            var relativeFile = Path.GetRelativePath(_absoluteWorkspacePath, file).Replace('\\', '/');
            if (_gitignore.IsIgnored(relativeFile, false))
            {
                Console.Error.WriteLine($"[WorkspaceParser] GitIgnore: Ignoring file '{relativeFile}'");
                continue;
            }

            var ext = Path.GetExtension(file).ToLowerInvariant();
            bool canParse = false;
            lock (Parsers)
            {
                canParse = Parsers.Any(p => p.CanParse(ext));
            }

            if (canParse)
            {
                if (!_projectFiles.TryGetValue(nextParentId, out var fileList))
                {
                    fileList = new List<string>();
                    _projectFiles[nextParentId] = fileList;
                }
                fileList.Add(file);
            }
        }

        // Recursively traverse subdirectories
        var subDirs = Directory.GetDirectories(currentDir);
        foreach (var subDir in subDirs)
        {
            var subProjectTypes = new HashSet<string>(activeProjectTypes);
            Scan(subDir, nextParentId, subProjectTypes, currentInsideProject);
        }
    }

    public async Task<(int NodesCount, int RelationshipsCount, Dictionary<string, int> NodesByKind)> IndexAsync()
    {
        // 1. Clear previous workspace data surgically if clear option is enabled
        if (_clear)
        {
            Console.Error.WriteLine($"[WorkspaceParser] Clearing workspace data for path '{_absoluteWorkspacePath}'...");
            await _dbClient.ClearWorkspaceAsync(_absoluteWorkspacePath);
        }

        // 2. Ensure database indexes exist
        await _dbClient.CreateIndicesAsync();

        var folderName = Path.GetFileName(_absoluteWorkspacePath);
        if (string.IsNullOrEmpty(folderName)) folderName = _absoluteWorkspacePath;

        // Create the Workspace Node immediately and upload it!
        var workspaceNode = new Database.Node(
            _workspaceNodeId,
            "Workspace",
            new Dictionary<string, object>
            {
                ["path"] = _absoluteWorkspacePath,
                ["name"] = folderName
            }
        );
        Console.Error.WriteLine($"[WorkspaceParser] Uploading Workspace node for '{_absoluteWorkspacePath}'...");
        await _dbClient.UploadNodesAsync(new List<Database.Node> { workspaceNode });

        // Run structural scanning to map folders, projects, and group files
        Console.Error.WriteLine("[WorkspaceParser] Scanning workspace directory structure...");
        Scan(_absoluteWorkspacePath, _workspaceNodeId, new HashSet<string>(), false);

        // Upload all structural nodes (Folder, Project) and structural relationships (CONTAINS)
        if (_structuralNodes.Count > 0)
        {
            Console.Error.WriteLine($"[WorkspaceParser] Uploading {_structuralNodes.Count} structural directory nodes...");
            await _dbClient.UploadNodesAsync(_structuralNodes);
        }
        if (_structuralRelationships.Count > 0)
        {
            Console.Error.WriteLine($"[WorkspaceParser] Uploading {_structuralRelationships.Count} structural CONTAINS relationships...");
            await _dbClient.UploadRelationshipsAsync(_structuralRelationships);
        }

        // Track indexing statistics
        _totalNodesCount += _structuralNodes.Count;
        _totalRelsCount += _structuralRelationships.Count;
        foreach (var node in _structuralNodes)
        {
            if (!_nodesByKind.ContainsKey(node.Kind)) _nodesByKind[node.Kind] = 0;
            _nodesByKind[node.Kind]++;
        }

        // 3. Process and parse files project-by-project/group-by-group, flushing them immediately
        foreach (var entry in _projectFiles)
        {
            var projectOrWorkspaceId = entry.Key;
            var filePaths = entry.Value;

            var groupNodes = new List<Database.Node>();
            var groupRelationships = new List<Database.Relationship>();
            var groupReferences = new List<Reference>();

            foreach (var file in filePaths)
            {
                var ext = Path.GetExtension(file).ToLower();

                ILanguageParser? langParser = null;
                lock (Parsers)
                {
                    langParser = Parsers.FirstOrDefault(p => p.CanParse(ext));
                }
                if (langParser == null) continue;

                var relativePath = Path.GetRelativePath(_absoluteWorkspacePath, file).Replace('\\', '/');
                Console.Error.WriteLine($"[WorkspaceParser] Parsing file '{relativePath}' using {langParser.LanguageName} parser...");

                try
                {
                    using var language = new Language(langParser.LanguageName);
                    using var parser = new global::TreeSitter.Parser(language);

                    var sourceText = File.ReadAllText(file);
                    using var tree = parser.Parse(sourceText);

                    if (tree == null || tree.RootNode == null) continue;

                    var ctx = new FileContext(_absoluteWorkspacePath, relativePath, sourceText, langParser);

                    // Add File Node
                    var fileNodeId = $"file:{_absoluteWorkspacePath}:{relativePath}";
                    ctx.Nodes.Add(new Database.Node(
                        fileNodeId,
                        "File",
                        new Dictionary<string, object>
                        {
                            ["path"] = relativePath,
                            ["name"] = Path.GetFileName(file)
                        }
                    ));

                    // Find the parent directory node info from _visitedDirs
                    var parentDir = Path.GetDirectoryName(file)!.Replace('\\', '/');
                    var parentRelative = Path.GetRelativePath(_absoluteWorkspacePath, parentDir).Replace('\\', '/');
                    if (parentRelative == ".") parentRelative = "";

                    string parentNodeId = _workspaceNodeId;
                    if (_visitedDirs.TryGetValue(parentRelative, out var parentInfo))
                    {
                        parentNodeId = parentInfo.Id;
                    }

                    // Relate Parent Node to File Node
                    groupRelationships.Add(new Database.Relationship(parentNodeId, fileNodeId, "CONTAINS"));

                    // Traverse AST
                    TraverseNode(tree.RootNode, fileNodeId, ctx);

                    groupNodes.AddRange(ctx.Nodes);
                    groupRelationships.AddRange(ctx.Relationships);
                    groupReferences.AddRange(ctx.References);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error parsing file {file}: {ex.Message}");
                }
            }

            // Immediately flush this project/group's files and AST nodes to the database
            if (groupNodes.Count > 0)
            {
                await _dbClient.UploadNodesAsync(groupNodes);
                _totalNodesCount += groupNodes.Count;

                foreach (var node in groupNodes)
                {
                    if (!_nodesByKind.ContainsKey(node.Kind)) _nodesByKind[node.Kind] = 0;
                    _nodesByKind[node.Kind]++;

                    // Track symbols globally for inter-project/workspace reference resolution
                    if (node.Kind is "Class" or "Function")
                    {
                        if (node.Properties.TryGetValue("name", out var nameVal) && nameVal is string nameStr)
                        {
                            _globalSymbols[(node.Kind, nameStr)] = node.Id;
                        }
                    }
                }
            }

            if (groupRelationships.Count > 0)
            {
                await _dbClient.UploadRelationshipsAsync(groupRelationships);
                _totalRelsCount += groupRelationships.Count;
            }

            // Collect references globally
            _globalReferences.AddRange(groupReferences);

            Console.Error.WriteLine($"[WorkspaceParser] Flushed group '{projectOrWorkspaceId}' to graph database. File count: {filePaths.Count}");
        }

        // 4. Deferred Global Reference Resolution & Final Reference Upload
        Console.Error.WriteLine($"[WorkspaceParser] Resolving {_globalReferences.Count} global cross-references...");
        var referenceRelationships = new List<Database.Relationship>();

        foreach (var refItem in _globalReferences)
        {
            if (refItem.Kind == "CALLS")
            {
                if (_globalSymbols.TryGetValue(("Function", refItem.TargetName), out var targetNodeId))
                {
                    referenceRelationships.Add(new Database.Relationship(refItem.ScopeSymbolId, targetNodeId, "CALLS"));
                }
            }
            else if (refItem.Kind == "USES_TYPE")
            {
                if (_globalSymbols.TryGetValue(("Class", refItem.TargetName), out var targetNodeId))
                {
                    referenceRelationships.Add(new Database.Relationship(refItem.ScopeSymbolId, targetNodeId, "USES_TYPE"));
                }
            }
            else if (refItem.Kind == "IMPLEMENTS" || refItem.Kind == "INHERITS_FROM")
            {
                if (_globalSymbols.TryGetValue(("Class", refItem.TargetName), out var targetNodeId))
                {
                    referenceRelationships.Add(new Database.Relationship(refItem.ScopeSymbolId, targetNodeId, refItem.Kind));
                }
            }
            else if (refItem.Kind == "POTENTIAL_TYPE")
            {
                if (_globalSymbols.TryGetValue(("Class", refItem.TargetName), out var targetNodeId))
                {
                    if (refItem.ScopeSymbolId != targetNodeId)
                    {
                        bool hasInheritance = referenceRelationships.Any(r =>
                            r.From == refItem.ScopeSymbolId &&
                            r.To == targetNodeId &&
                            (r.Kind == "IMPLEMENTS" || r.Kind == "INHERITS_FROM"));

                        if (!hasInheritance)
                        {
                            referenceRelationships.Add(new Database.Relationship(refItem.ScopeSymbolId, targetNodeId, "USES_TYPE"));
                        }
                    }
                }
            }
        }

        if (referenceRelationships.Count > 0)
        {
            Console.Error.WriteLine($"[WorkspaceParser] Uploading {referenceRelationships.Count} resolved reference relationships...");
            await _dbClient.UploadRelationshipsAsync(referenceRelationships);
            _totalRelsCount += referenceRelationships.Count;
        }

        return (_totalNodesCount, _totalRelsCount, _nodesByKind);
    }

    private static void TraverseNode(Node node, string parentId, FileContext ctx)
    {
        string? kind = ctx.Parser.MapNodeType(node.Type);
        string? name = null;

        if (kind != null)
        {
            name = ctx.Parser.ExtractIdentifier(node);
        }

        string currentParentId = parentId;

        if (kind != null && !string.IsNullOrEmpty(name))
        {
            var symbolId = $"symbol:{ctx.WorkspacePath}:{ctx.FilePath}:{kind}:{name}:{node.StartPosition.Row}";
            var properties = new Dictionary<string, object>
            {
                ["name"] = name,
                ["symbol"] = symbolId,
                ["start_line"] = node.StartPosition.Row,
                ["start_col"] = node.StartPosition.Column,
                ["end_line"] = node.EndPosition.Row,
                ["end_col"] = node.EndPosition.Column,
                ["file_path"] = ctx.FilePath
            };

            ctx.Nodes.Add(new Database.Node(symbolId, kind, properties));
            ctx.Relationships.Add(new Database.Relationship(parentId, symbolId, "CONTAINS"));
            currentParentId = symbolId;
        }

        // Collect references inside the current symbol scope
        if (currentParentId.StartsWith("symbol:"))
        {
            if (node.Type is "identifier" or "type_identifier")
            {
                ctx.References.Add(new Reference(currentParentId, node.Text, "POTENTIAL_TYPE"));
            }

            ctx.Parser.CollectReferences(node, currentParentId, ctx.References);
        }

        foreach (var child in node.Children)
        {
            TraverseNode(child, currentParentId, ctx);
        }
    }

    private class GitIgnoreMatcher
    {
        private readonly List<(string Pattern, System.Text.RegularExpressions.Regex Regex, bool IsDirectoryOnly)> _rules = new();

        public GitIgnoreMatcher(string workspaceRoot)
        {
            var gitignorePath = Path.Combine(workspaceRoot, ".gitignore");
            if (!File.Exists(gitignorePath)) return;

            foreach (var line in File.ReadLines(gitignorePath))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#')) continue;

                bool isDirectoryOnly = false;
                if (trimmed.EndsWith('/'))
                {
                    isDirectoryOnly = true;
                    trimmed = trimmed.Substring(0, trimmed.Length - 1);
                }

                bool isAnchored = false;
                if (trimmed.StartsWith('/'))
                {
                    isAnchored = true;
                    trimmed = trimmed.Substring(1);
                }

                var escaped = System.Text.RegularExpressions.Regex.Escape(trimmed);
                var regexPattern = escaped
                    .Replace("\\*", ".*")
                    .Replace("\\?", ".");

                if (isAnchored)
                {
                    regexPattern = "^" + regexPattern;
                }
                else
                {
                    regexPattern = "(^|/)" + regexPattern;
                }

                if (isDirectoryOnly)
                {
                    regexPattern += "($|/)";
                }
                else
                {
                    regexPattern += "($|/|\\.)";
                }

                try
                {
                    var regex = new System.Text.RegularExpressions.Regex(regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);
                    _rules.Add((trimmed, regex, isDirectoryOnly));
                }
                catch
                {
                    // Ignore malformed patterns
                }
            }
        }

        public bool IsIgnored(string relativePath, bool isDirectory)
        {
            relativePath = relativePath.Replace('\\', '/').Trim('/');
            if (string.IsNullOrEmpty(relativePath)) return false;

            foreach (var rule in _rules)
            {
                if (rule.IsDirectoryOnly && !isDirectory) continue;

                if (rule.Regex.IsMatch(relativePath))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
