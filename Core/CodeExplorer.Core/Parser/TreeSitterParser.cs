using TreeSitter;

namespace CodeExplorer.Parser;

public class SolutionParser
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

    private static void Scan(
        string currentDir, 
        string absoluteWorkspacePath, 
        string currentParentId,
        List<Database.Node> structuralNodes,
        List<Database.Relationship> structuralRelationships,
        Dictionary<string, List<string>> projectFiles,
        HashSet<string> detectedProjectTypes,
        Dictionary<string, (string Id, string Kind)> visitedDirs,
        bool insideProject,
        GitIgnoreMatcher gitignore)
    {
        var relativeDir = Path.GetRelativePath(absoluteWorkspacePath, currentDir).Replace('\\', '/');
        if (relativeDir == ".") relativeDir = "";

        // 1. Check GitIgnore exclusions first
        if (!string.IsNullOrEmpty(relativeDir) && gitignore.IsIgnored(relativeDir, true))
        {
            Console.Error.WriteLine($"[SolutionParser] GitIgnore: Ignoring directory '{relativeDir}'");
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
            Console.Error.WriteLine($"[SolutionParser] Generic: Skipping VCS/IDE folder '{relativeDir}'");
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
                        Console.Error.WriteLine($"[SolutionParser] Project: Detected {parser.ProjectType} project signature at '{relativeDir}'");
                    }
                }
            }
        }

        // Propagate whether we are inside a project to subdirectories
        bool currentInsideProject = insideProject || isProject;

        // 4. Add to detected project types
        foreach (var type in newlyDetectedTypes)
        {
            detectedProjectTypes.Add(type);
        }

        // 5. Check if current directory name should be excluded based on active project types and language exclusions
        bool shouldExclude = false;
        string? matchedExclusionFolder = null;
        string? matchedExclusionType = null;
        lock (Parsers)
        {
            foreach (var type in detectedProjectTypes)
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
            Console.Error.WriteLine($"[SolutionParser] Exclusion: Skipping directory '{relativeDir}' (matches language exclusion '{matchedExclusionFolder}' for '{matchedExclusionType}' project type)");
            return;
        }

        // Register current directory in visitedDirs and structuralNodes
        string currentId;
        string currentKind;

        if (string.IsNullOrEmpty(relativeDir))
        {
            currentId = $"solution:{absoluteWorkspacePath}";
            currentKind = "Solution";
            visitedDirs[relativeDir] = (currentId, currentKind);
        }
        else
        {
            if (isProject)
            {
                currentId = $"project:{absoluteWorkspacePath}:{relativeDir}";
                currentKind = "Project";
                structuralNodes.Add(new Database.Node(currentId, "Project", new Dictionary<string, object> 
                { 
                    ["name"] = dirName,
                    ["path"] = relativeDir,
                    ["project_type"] = projectType ?? "unknown"
                }));
                Console.Error.WriteLine($"[SolutionParser] Mapping directory '{relativeDir}' as Project Node");
            }
            else
            {
                currentId = $"folder:{absoluteWorkspacePath}:{relativeDir}";
                currentKind = "Folder";
                structuralNodes.Add(new Database.Node(currentId, "Folder", new Dictionary<string, object> 
                { 
                    ["name"] = dirName,
                    ["path"] = relativeDir 
                }));
            }

            visitedDirs[relativeDir] = (currentId, currentKind);

            // Establish relationship from Parent Directory to Current Directory
            var parentPath = Path.GetDirectoryName(currentDir)!.Replace('\\', '/');
            var parentRelative = Path.GetRelativePath(absoluteWorkspacePath, parentPath).Replace('\\', '/');
            if (parentRelative == ".") parentRelative = "";

            if (visitedDirs.TryGetValue(parentRelative, out var parentInfo))
            {
                structuralRelationships.Add(new Database.Relationship(parentInfo.Id, currentId, "CONTAINS"));
            }
        }

        string nextParentId = isProject ? currentId : currentParentId;

        // Add matching source files in this folder
        foreach (var file in filesInDir)
        {
            var relativeFile = Path.GetRelativePath(absoluteWorkspacePath, file).Replace('\\', '/');
            if (gitignore.IsIgnored(relativeFile, false))
            {
                Console.Error.WriteLine($"[SolutionParser] GitIgnore: Ignoring file '{relativeFile}'");
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
                if (!projectFiles.TryGetValue(nextParentId, out var fileList))
                {
                    fileList = new List<string>();
                    projectFiles[nextParentId] = fileList;
                }
                fileList.Add(file);
            }
        }

        // Recursively traverse subdirectories
        var subDirs = Directory.GetDirectories(currentDir);
        foreach (var subDir in subDirs)
        {
            var subProjectTypes = new HashSet<string>(detectedProjectTypes);
            Scan(subDir, absoluteWorkspacePath, nextParentId, structuralNodes, structuralRelationships, projectFiles, subProjectTypes, visitedDirs, currentInsideProject, gitignore);
        }
    }

    public static async Task<(int NodesCount, int RelationshipsCount, Dictionary<string, int> NodesByKind)> IndexDirectoryAsync(
        string dirPath, 
        Database.MemgraphClient dbClient, 
        bool clear)
    {
        var absoluteWorkspacePath = Path.GetFullPath(dirPath).Replace('\\', '/');
        
        // 1. Clear previous workspace data surgically if clear option is enabled
        if (clear)
        {
            Console.Error.WriteLine($"[SolutionParser] Clearing workspace data for path '{absoluteWorkspacePath}'...");
            await dbClient.ClearWorkspaceAsync(absoluteWorkspacePath);
        }

        // 2. Ensure database indexes exist
        await dbClient.CreateIndicesAsync();

        var folderName = Path.GetFileName(absoluteWorkspacePath);
        if (string.IsNullOrEmpty(folderName)) folderName = absoluteWorkspacePath;
        var solutionNodeId = $"solution:{absoluteWorkspacePath}";

        // Create the Solution Node immediately and upload it!
        var solutionNode = new Database.Node(
            solutionNodeId,
            "Solution",
            new Dictionary<string, object>
            {
                ["path"] = absoluteWorkspacePath,
                ["name"] = folderName
            }
        );
        Console.Error.WriteLine($"[SolutionParser] Uploading Solution node for '{absoluteWorkspacePath}'...");
        await dbClient.UploadNodesAsync(new List<Database.Node> { solutionNode });

        // Prepare lists for structural discovery
        var structuralNodes = new List<Database.Node>();
        var structuralRelationships = new List<Database.Relationship>();
        var projectFiles = new Dictionary<string, List<string>>();
        var detectedProjectTypes = new HashSet<string>();
        var visitedDirs = new Dictionary<string, (string Id, string Kind)>();
        var gitignore = new GitIgnoreMatcher(absoluteWorkspacePath);

        // Run structural scanning to map folders, projects, and group files
        Console.Error.WriteLine("[SolutionParser] Scanning workspace directory structure...");
        Scan(absoluteWorkspacePath, absoluteWorkspacePath, solutionNodeId, structuralNodes, structuralRelationships, projectFiles, detectedProjectTypes, visitedDirs, false, gitignore);

        // Upload all structural nodes (Folder, Project) and structural relationships (CONTAINS)
        if (structuralNodes.Count > 0)
        {
            Console.Error.WriteLine($"[SolutionParser] Uploading {structuralNodes.Count} structural directory nodes...");
            await dbClient.UploadNodesAsync(structuralNodes);
        }
        if (structuralRelationships.Count > 0)
        {
            Console.Error.WriteLine($"[SolutionParser] Uploading {structuralRelationships.Count} structural CONTAINS relationships...");
            await dbClient.UploadRelationshipsAsync(structuralRelationships);
        }

        // Track indexing statistics
        int totalNodesCount = 1 + structuralNodes.Count; // 1 for the Solution node itself
        int totalRelsCount = structuralRelationships.Count;
        var nodesByKind = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Solution"] = 1
        };
        foreach (var node in structuralNodes)
        {
            if (!nodesByKind.ContainsKey(node.Kind)) nodesByKind[node.Kind] = 0;
            nodesByKind[node.Kind]++;
        }

        // Global symbols and references for inter-project resolution
        var globalSymbols = new Dictionary<(string Kind, string Name), string>();
        var globalReferences = new List<Reference>();

        // 3. Process and parse files project-by-project/group-by-group, flushing them immediately
        foreach (var entry in projectFiles)
        {
            var projectOrSolutionId = entry.Key;
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

                var relativePath = Path.GetRelativePath(dirPath, file).Replace('\\', '/');
                Console.Error.WriteLine($"[SolutionParser] Parsing file '{relativePath}' using {langParser.LanguageName} parser...");

                try
                {
                    using var language = new Language(langParser.LanguageName);
                    using var parser = new global::TreeSitter.Parser(language);

                    var sourceText = File.ReadAllText(file);
                    using var tree = parser.Parse(sourceText);

                    if (tree == null || tree.RootNode == null) continue;

                    var ctx = new FileContext(absoluteWorkspacePath, relativePath, sourceText, langParser);

                    // Add File Node
                    var fileNodeId = $"file:{absoluteWorkspacePath}:{relativePath}";
                    ctx.Nodes.Add(new Database.Node(
                        fileNodeId,
                        "File",
                        new Dictionary<string, object>
                        {
                            ["path"] = relativePath,
                            ["name"] = Path.GetFileName(file)
                        }
                    ));

                    // Find the parent directory node info from visitedDirs
                    var parentDir = Path.GetDirectoryName(file)!.Replace('\\', '/');
                    var parentRelative = Path.GetRelativePath(dirPath, parentDir).Replace('\\', '/');
                    if (parentRelative == ".") parentRelative = "";

                    string parentNodeId = solutionNodeId;
                    if (visitedDirs.TryGetValue(parentRelative, out var parentInfo))
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
                await dbClient.UploadNodesAsync(groupNodes);
                totalNodesCount += groupNodes.Count;

                foreach (var node in groupNodes)
                {
                    if (!nodesByKind.ContainsKey(node.Kind)) nodesByKind[node.Kind] = 0;
                    nodesByKind[node.Kind]++;

                    // Track symbols globally for inter-project/solution reference resolution
                    if (node.Kind is "Class" or "Function")
                    {
                        if (node.Properties.TryGetValue("name", out var nameVal) && nameVal is string nameStr)
                        {
                            globalSymbols[(node.Kind, nameStr)] = node.Id;
                        }
                    }
                }
            }

            if (groupRelationships.Count > 0)
            {
                await dbClient.UploadRelationshipsAsync(groupRelationships);
                totalRelsCount += groupRelationships.Count;
            }

            // Collect references globally
            globalReferences.AddRange(groupReferences);

            Console.Error.WriteLine($"[SolutionParser] Flushed group '{projectOrSolutionId}' to graph database. File count: {filePaths.Count}");
        }

        // 4. Deferred Global Reference Resolution & Final Reference Upload
        Console.Error.WriteLine($"[SolutionParser] Resolving {globalReferences.Count} global cross-references...");
        var referenceRelationships = new List<Database.Relationship>();

        foreach (var refItem in globalReferences)
        {
            if (refItem.Kind == "CALLS")
            {
                if (globalSymbols.TryGetValue(("Function", refItem.TargetName), out var targetNodeId))
                {
                    referenceRelationships.Add(new Database.Relationship(refItem.ScopeSymbolId, targetNodeId, "CALLS"));
                }
            }
            else if (refItem.Kind == "USES_TYPE")
            {
                if (globalSymbols.TryGetValue(("Class", refItem.TargetName), out var targetNodeId))
                {
                    referenceRelationships.Add(new Database.Relationship(refItem.ScopeSymbolId, targetNodeId, "USES_TYPE"));
                }
            }
            else if (refItem.Kind == "IMPLEMENTS" || refItem.Kind == "INHERITS_FROM")
            {
                if (globalSymbols.TryGetValue(("Class", refItem.TargetName), out var targetNodeId))
                {
                    referenceRelationships.Add(new Database.Relationship(refItem.ScopeSymbolId, targetNodeId, refItem.Kind));
                }
            }
            else if (refItem.Kind == "POTENTIAL_TYPE")
            {
                if (globalSymbols.TryGetValue(("Class", refItem.TargetName), out var targetNodeId))
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
            Console.Error.WriteLine($"[SolutionParser] Uploading {referenceRelationships.Count} resolved reference relationships...");
            await dbClient.UploadRelationshipsAsync(referenceRelationships);
            totalRelsCount += referenceRelationships.Count;
        }

        return (totalNodesCount, totalRelsCount, nodesByKind);
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
