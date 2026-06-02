using System;
using CodeExplorer.Common;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;
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
    private readonly List<Database.Relationship> _globalProjectDependencies = new();
    private readonly Dictionary<string, int> _nodesByKind = new(StringComparer.OrdinalIgnoreCase);

    private int _totalNodesCount;
    private int _totalRelsCount;

    private readonly Channel<Func<Task>> _sharedChannel;
    private readonly HashSet<string> _excludedSubdirectories = new(StringComparer.OrdinalIgnoreCase);

    private WorkspaceParser(
        string dirPath, 
        Database.MemgraphClient dbClient, 
        bool clear,
        Channel<Func<Task>> sharedChannel,
        Dictionary<(string Kind, string Name), string> globalSymbols,
        List<Reference> globalReferences,
        List<Database.Relationship> globalProjectDependencies)
    {
        _absoluteWorkspacePath = Path.GetFullPath(dirPath).Replace('\\', '/');
        _dbClient = dbClient;
        _clear = clear;
        _gitignore = new GitIgnoreMatcher(_absoluteWorkspacePath);

        var folderName = Path.GetFileName(_absoluteWorkspacePath);
        if (string.IsNullOrEmpty(folderName)) folderName = _absoluteWorkspacePath;
        _workspaceNodeId = $"workspace:{_absoluteWorkspacePath}";

        _sharedChannel = sharedChannel;
        _globalSymbols = globalSymbols;
        _globalReferences = globalReferences;
        _globalProjectDependencies = globalProjectDependencies;

        _nodesByKind[OntologyConstants.NodeLabels.Workspace] = 1;
        _totalNodesCount = 1; // Workspace node
    }

    public WorkspaceParser(string dirPath, Database.MemgraphClient dbClient, bool clear)
    {
        _absoluteWorkspacePath = Path.GetFullPath(dirPath).Replace('\\', '/');
        _dbClient = dbClient;
        _clear = clear;
        _gitignore = new GitIgnoreMatcher(_absoluteWorkspacePath);

        var folderName = Path.GetFileName(_absoluteWorkspacePath);
        if (string.IsNullOrEmpty(folderName)) folderName = _absoluteWorkspacePath;
        _workspaceNodeId = $"workspace:{_absoluteWorkspacePath}";

        _sharedChannel = Channel.CreateUnbounded<Func<Task>>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false }
        );
        _globalSymbols = new Dictionary<(string Kind, string Name), string>();
        _globalReferences = new List<Reference>();
        _globalProjectDependencies = new List<Database.Relationship>();

        _nodesByKind[OntologyConstants.NodeLabels.Workspace] = 1;
        _totalNodesCount = 1; // Workspace node
    }

    private void Scan(string currentDir, string currentParentId, HashSet<string> activeProjectTypes, bool insideProject)
    {
        if (_excludedSubdirectories.Contains(currentDir))
        {
            Console.Error.WriteLine($"[WorkspaceParser] Master: Skipping project directory '{currentDir}' (will be indexed independently)");
            return;
        }

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
                var projectNodeId = $"project:{_absoluteWorkspacePath}:";
                _structuralNodes.Add(new Database.Node(projectNodeId, OntologyConstants.NodeLabels.Project, new Dictionary<string, object> 
                { 
                    ["name"] = dirName,
                    ["path"] = "",
                    ["project_type"] = projectType ?? "unknown"
                }));
                _structuralRelationships.Add(new Database.Relationship(currentParentId, projectNodeId, OntologyConstants.Relationships.Contains));
                Console.Error.WriteLine($"[WorkspaceParser] Created independent Project Node linked to parent container: '{projectNodeId}'");
                
                currentId = projectNodeId;
                currentKind = OntologyConstants.NodeLabels.Project;
            }
            else
            {
                currentId = _workspaceNodeId;
                currentKind = OntologyConstants.NodeLabels.Workspace;
            }
            _visitedDirs[relativeDir] = (currentId, currentKind);
        }
        else
        {
            if (isProject)
            {
                var projectNodeId = $"project:{_absoluteWorkspacePath}:{relativeDir}";
                _structuralNodes.Add(new Database.Node(projectNodeId, OntologyConstants.NodeLabels.Project, new Dictionary<string, object> 
                { 
                    ["name"] = dirName,
                    ["path"] = dirName,
                    ["project_type"] = projectType ?? "unknown"
                }));
                _structuralRelationships.Add(new Database.Relationship(currentParentId, projectNodeId, OntologyConstants.Relationships.Contains));
                Console.Error.WriteLine($"[WorkspaceParser] Created independent Project Node linked to parent container: '{projectNodeId}'");

                currentId = projectNodeId;
                currentKind = OntologyConstants.NodeLabels.Project;
            }
            else if (insideProject)
            {
                currentId = $"projectfolder:{_absoluteWorkspacePath}:{relativeDir}";
                currentKind = OntologyConstants.NodeLabels.ProjectFolder;
                _structuralNodes.Add(new Database.Node(currentId, OntologyConstants.NodeLabels.ProjectFolder, new Dictionary<string, object> 
                { 
                    ["name"] = dirName,
                    ["path"] = dirName 
                }));
                _structuralRelationships.Add(new Database.Relationship(currentParentId, currentId, OntologyConstants.Relationships.Contains));
            }
            else
            {
                currentId = $"workspacefolder:{_absoluteWorkspacePath}:{relativeDir}";
                currentKind = OntologyConstants.NodeLabels.WorkspaceFolder;
                _structuralNodes.Add(new Database.Node(currentId, OntologyConstants.NodeLabels.WorkspaceFolder, new Dictionary<string, object> 
                { 
                    ["name"] = dirName,
                    ["path"] = dirName 
                }));
                _structuralRelationships.Add(new Database.Relationship(currentParentId, currentId, OntologyConstants.Relationships.Contains));
            }

            _visitedDirs[relativeDir] = (currentId, currentKind);
        }

        string nextParentId = currentId;

        if (currentInsideProject)
        {
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
        }

        // Recursively traverse subdirectories
        var subDirs = Directory.GetDirectories(currentDir);
        foreach (var subDir in subDirs)
        {
            var subProjectTypes = new HashSet<string>(activeProjectTypes);
            Scan(subDir, nextParentId, subProjectTypes, currentInsideProject);
        }
    }

    private List<string> FindProjectDirectories()
    {
        var projectDirs = new List<string>();
        // Let's check if the root directory itself is a project
        var rootFiles = Directory.GetFiles(_absoluteWorkspacePath);
        bool rootIsProject = false;
        lock (Parsers)
        {
            foreach (var parser in Parsers)
            {
                if (parser.IsProjectDirectory(_absoluteWorkspacePath, rootFiles))
                {
                    rootIsProject = true;
                    break;
                }
            }
        }

        if (rootIsProject)
        {
            // Root is the project, so no need to search for subprojects.
            return projectDirs;
        }

        FindProjectDirsInternal(_absoluteWorkspacePath, projectDirs);
        return projectDirs;
    }

    private void FindProjectDirsInternal(string currentDir, List<string> projectDirs)
    {
        var relativeDir = Path.GetRelativePath(_absoluteWorkspacePath, currentDir).Replace('\\', '/');
        if (relativeDir == ".") relativeDir = "";

        if (!string.IsNullOrEmpty(relativeDir))
        {
            if (_gitignore.IsIgnored(relativeDir, true)) return;

            var dirNameLower = Path.GetFileName(currentDir).ToLowerInvariant();
            var genericExclusions = new HashSet<string> { ".git", ".github", ".vscode", ".idea", "node_modules", "bin", "obj" };
            if (genericExclusions.Contains(dirNameLower)) return;
        }

        var filesInDir = Directory.GetFiles(currentDir);
        bool isProject = false;
        lock (Parsers)
        {
            foreach (var parser in Parsers)
            {
                if (parser.IsProjectDirectory(currentDir, filesInDir))
                {
                    isProject = true;
                    break;
                }
            }
        }

        if (isProject)
        {
            projectDirs.Add(currentDir);
            return; // Stop recursing into project subdirectories
        }

        foreach (var subDir in Directory.GetDirectories(currentDir))
        {
            FindProjectDirsInternal(subDir, projectDirs);
        }
    }

    private async Task ParseProjectDependenciesAsync(string projectDir, string projectNodeId)
    {
        // 1. Parse C# csproj files
        var csprojFiles = Directory.GetFiles(projectDir, "*.csproj", SearchOption.AllDirectories);
        foreach (var csprojFile in csprojFiles)
        {
            try
            {
                var content = await File.ReadAllTextAsync(csprojFile);
                var doc = System.Xml.Linq.XDocument.Parse(content);
                
                // Extract local project references
                var projectRefs = doc.Descendants("ProjectReference");
                foreach (var pref in projectRefs)
                {
                    var include = pref.Attribute("Include")?.Value;
                    if (string.IsNullOrEmpty(include)) continue;

                    var referencedCsprojPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(csprojFile)!, include)).Replace('\\', '/');
                    var referencedProjectDir = Path.GetFullPath(Path.GetDirectoryName(referencedCsprojPath)!).Replace('\\', '/');
                    var targetProjectNodeId = $"project:{referencedProjectDir}:";

                    lock (_globalProjectDependencies)
                    {
                        _globalProjectDependencies.Add(new Database.Relationship(projectNodeId, targetProjectNodeId, OntologyConstants.Relationships.DependsOn));
                    }
                }

                // Extract NuGet package references
                var packageRefs = doc.Descendants("PackageReference");
                foreach (var packRef in packageRefs)
                {
                    var name = packRef.Attribute("Include")?.Value;
                    var version = packRef.Attribute("Version")?.Value ?? packRef.Element("Version")?.Value ?? "unknown";
                    if (string.IsNullOrEmpty(name)) continue;

                    var packageNodeId = $"package:{name.ToLowerInvariant()}";
                    var packageNode = new Database.Node(packageNodeId, OntologyConstants.NodeLabels.Package, new Dictionary<string, object>
                    {
                        ["name"] = name,
                        ["version"] = version,
                        ["type"] = "nuget"
                    });

                    // Queue node and relationship
                    await _sharedChannel.Writer.WriteAsync(() => _dbClient.UploadNodesAsync(new List<Database.Node> { packageNode }));
                    var rel = new Database.Relationship(projectNodeId, packageNodeId, OntologyConstants.Relationships.DependsOn);
                    await _sharedChannel.Writer.WriteAsync(() => _dbClient.UploadRelationshipsAsync(new List<Database.Relationship> { rel }));
                    
                    lock (_nodesByKind)
                    {
                        if (!_nodesByKind.ContainsKey(OntologyConstants.NodeLabels.Package)) _nodesByKind[OntologyConstants.NodeLabels.Package] = 0;
                        _nodesByKind[OntologyConstants.NodeLabels.Package]++;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[WorkspaceParser] Error parsing C# dependencies in '{csprojFile}': {ex.Message}");
            }
        }

        // 2. Parse JS/TS package.json files
        var packageJsonFiles = Directory.GetFiles(projectDir, "package.json", SearchOption.AllDirectories);
        foreach (var packageJsonFile in packageJsonFiles)
        {
            try
            {
                var content = await File.ReadAllTextAsync(packageJsonFile);
                using var doc = System.Text.Json.JsonDocument.Parse(content);
                var root = doc.RootElement;

                var depProperties = new[] { "dependencies", "devDependencies" };
                foreach (var propName in depProperties)
                {
                    if (root.TryGetProperty(propName, out var depsObj) && depsObj.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        foreach (var prop in depsObj.EnumerateObject())
                        {
                            var packageName = prop.Name;
                            var packageVersion = prop.Value.GetString() ?? "unknown";

                            // Check if it is a local workspace project reference
                            if (packageVersion.StartsWith("file:") || packageVersion.StartsWith("workspace:"))
                            {
                                // Local relative reference, we can try to resolve it if possible
                                var relativePath = packageVersion.Substring(packageVersion.IndexOf(':') + 1);
                                if (!string.IsNullOrEmpty(relativePath))
                                {
                                    var referencedDir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(packageJsonFile)!, relativePath)).Replace('\\', '/');
                                    var targetProjectNodeId = $"project:{referencedDir}:";
                                    lock (_globalProjectDependencies)
                                    {
                                        _globalProjectDependencies.Add(new Database.Relationship(projectNodeId, targetProjectNodeId, OntologyConstants.Relationships.DependsOn));
                                    }
                                    continue;
                                }
                            }

                            // Treat as npm package reference
                            var packageNodeId = $"package:{packageName.ToLowerInvariant()}";
                            var packageNode = new Database.Node(packageNodeId, OntologyConstants.NodeLabels.Package, new Dictionary<string, object>
                            {
                                ["name"] = packageName,
                                ["version"] = packageVersion,
                                ["type"] = "npm"
                            });

                            await _sharedChannel.Writer.WriteAsync(() => _dbClient.UploadNodesAsync(new List<Database.Node> { packageNode }));
                            var npmRel = new Database.Relationship(projectNodeId, packageNodeId, OntologyConstants.Relationships.DependsOn);
                            await _sharedChannel.Writer.WriteAsync(() => _dbClient.UploadRelationshipsAsync(new List<Database.Relationship> { npmRel }));

                            lock (_nodesByKind)
                            {
                                if (!_nodesByKind.ContainsKey(OntologyConstants.NodeLabels.Package)) _nodesByKind[OntologyConstants.NodeLabels.Package] = 0;
                                _nodesByKind[OntologyConstants.NodeLabels.Package]++;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[WorkspaceParser] Error parsing JS/TS dependencies in '{packageJsonFile}': {ex.Message}");
            }
        }

        // 3. Delegate produced package extraction to all active parsers
        bool packageDetected = false;
        List<ILanguageParser> activeParsers;
        lock (Parsers)
        {
            activeParsers = new List<ILanguageParser>(Parsers);
        }

        foreach (var parser in activeParsers)
        {
            try
            {
                var producedPackage = await parser.GetProducedPackageAsync(projectDir);
                if (producedPackage != null)
                {
                    var packageNodeId = $"package:{producedPackage.Name.ToLowerInvariant()}";
                    var packageNode = new Database.Node(packageNodeId, OntologyConstants.NodeLabels.Package, new Dictionary<string, object>
                    {
                        ["name"] = producedPackage.Name,
                        ["version"] = producedPackage.Version,
                        ["type"] = producedPackage.Type
                    });

                    await _sharedChannel.Writer.WriteAsync(() => _dbClient.UploadNodesAsync(new List<Database.Node> { packageNode }));
                    var implRel = new Database.Relationship(packageNodeId, projectNodeId, OntologyConstants.Relationships.ImplementedBy);
                    await _sharedChannel.Writer.WriteAsync(() => _dbClient.UploadRelationshipsAsync(new List<Database.Relationship> { implRel }));

                    lock (_nodesByKind)
                    {
                        if (!_nodesByKind.ContainsKey(OntologyConstants.NodeLabels.Package)) _nodesByKind[OntologyConstants.NodeLabels.Package] = 0;
                        _nodesByKind[OntologyConstants.NodeLabels.Package]++;
                    }

                    packageDetected = true;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[WorkspaceParser] Error getting produced package from {parser.ProjectType} parser in '{projectDir}': {ex.Message}");
            }
        }

        // 4. Default project folder package name fallback (if no parser detected any produced package)
        if (!packageDetected)
        {
            var dirName = Path.GetFileName(projectDir);
            if (!string.IsNullOrEmpty(dirName))
            {
                var packageNodeId = $"package:{dirName.ToLowerInvariant()}";
                var packageNode = new Database.Node(packageNodeId, OntologyConstants.NodeLabels.Package, new Dictionary<string, object>
                {
                    ["name"] = dirName,
                    ["version"] = "1.0.0",
                    ["type"] = "unknown"
                });

                await _sharedChannel.Writer.WriteAsync(() => _dbClient.UploadNodesAsync(new List<Database.Node> { packageNode }));
                var implRel = new Database.Relationship(packageNodeId, projectNodeId, OntologyConstants.Relationships.ImplementedBy);
                await _sharedChannel.Writer.WriteAsync(() => _dbClient.UploadRelationshipsAsync(new List<Database.Relationship> { implRel }));

                lock (_nodesByKind)
                {
                    if (!_nodesByKind.ContainsKey(OntologyConstants.NodeLabels.Package)) _nodesByKind[OntologyConstants.NodeLabels.Package] = 0;
                    _nodesByKind[OntologyConstants.NodeLabels.Package]++;
                }
            }
        }
    }

    private async Task<(int NodesCount, int RelationshipsCount, Dictionary<string, int> NodesByKind)> IndexProjectInternalAsync()
    {
        // 1. Clear previous project workspace data surgically if clear option is enabled
        if (_clear)
        {
            Console.Error.WriteLine($"[WorkspaceParser] Clearing project workspace data for path '{_absoluteWorkspacePath}'...");
            await _dbClient.ClearWorkspaceAsync(_absoluteWorkspacePath);
        }

        var folderName = Path.GetFileName(_absoluteWorkspacePath);
        if (string.IsNullOrEmpty(folderName)) folderName = _absoluteWorkspacePath;

        // Create the Workspace Node immediately and upload it!
        var workspaceNode = new Database.Node(
            _workspaceNodeId,
            OntologyConstants.NodeLabels.Workspace,
            new Dictionary<string, object>
            {
                ["path"] = _absoluteWorkspacePath,
                ["name"] = folderName
            }
        );
        
        await _sharedChannel.Writer.WriteAsync(() => _dbClient.UploadNodesAsync(new List<Database.Node> { workspaceNode }));

        // Run structural scanning to map folders, projects, and group files
        Scan(_absoluteWorkspacePath, _workspaceNodeId, new HashSet<string>(), false);

        // Upload all structural nodes (Folder, Project) and structural relationships (CONTAINS)
        if (_structuralNodes.Count > 0)
        {
            await _sharedChannel.Writer.WriteAsync(() => _dbClient.UploadNodesAsync(_structuralNodes));
        }
        if (_structuralRelationships.Count > 0)
        {
            await _sharedChannel.Writer.WriteAsync(() => _dbClient.UploadRelationshipsAsync(_structuralRelationships));
        }

        // Track indexing statistics
        _totalNodesCount += _structuralNodes.Count;
        _totalRelsCount += _structuralRelationships.Count;
        foreach (var node in _structuralNodes)
        {
            if (!_nodesByKind.ContainsKey(node.Kind)) _nodesByKind[node.Kind] = 0;
            _nodesByKind[node.Kind]++;
        }

        // Process and parse files project-by-project/group-by-group, flushing them immediately
        var activeLanguages = new Dictionary<string, Language>();
        var activeParsers = new Dictionary<string, global::TreeSitter.Parser>();

        try
        {
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

                    try
                    {
                        if (!activeLanguages.TryGetValue(langParser.LanguageName, out var language))
                        {
                            language = new Language(langParser.LanguageName);
                            activeLanguages[langParser.LanguageName] = language;
                        }

                        if (!activeParsers.TryGetValue(langParser.LanguageName, out var parser))
                        {
                            parser = new global::TreeSitter.Parser(language);
                            activeParsers[langParser.LanguageName] = parser;
                        }

                        Console.Error.WriteLine($"[WorkspaceParser] Parsing file: '{relativePath}' ({langParser.ProjectType})");
                        var sourceText = File.ReadAllText(file);
                        using var tree = parser.Parse(sourceText);

                        if (tree == null || tree.RootNode == null) continue;

                        var ctx = new FileContext(_absoluteWorkspacePath, relativePath, sourceText, langParser);

                        // Add File Node
                        var fileNodeId = $"file:{_absoluteWorkspacePath}:{relativePath}";
                         ctx.Nodes.Add(new Database.Node(
                            fileNodeId,
                            OntologyConstants.NodeLabels.File,
                            new Dictionary<string, object>
                            {
                                ["path"] = Path.GetFileName(file),
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
                        groupRelationships.Add(new Database.Relationship(parentNodeId, fileNodeId, OntologyConstants.Relationships.Contains));

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

                if (groupNodes.Count > 0)
                {
                    var nodesToUpload = groupNodes;
                    await _sharedChannel.Writer.WriteAsync(() => _dbClient.UploadNodesAsync(nodesToUpload));
                    _totalNodesCount += groupNodes.Count;

                    foreach (var node in groupNodes)
                    {
                        if (!_nodesByKind.ContainsKey(node.Kind)) _nodesByKind[node.Kind] = 0;
                        _nodesByKind[node.Kind]++;

                        // Track symbols globally for inter-project/workspace reference resolution
                        if (node.Kind == OntologyConstants.NodeLabels.Class || node.Kind == OntologyConstants.NodeLabels.Interface || node.Kind == OntologyConstants.NodeLabels.Function)
                        {
                            if (node.Properties.TryGetValue("name", out var nameVal) && nameVal is string nameStr)
                            {
                                lock (_globalSymbols)
                                {
                                    _globalSymbols[(node.Kind, nameStr)] = node.Id;
                                }
                            }
                        }
                    }
                }

                if (groupRelationships.Count > 0)
                {
                    var relsToUpload = groupRelationships;
                    await _sharedChannel.Writer.WriteAsync(() => _dbClient.UploadRelationshipsAsync(relsToUpload));
                    _totalRelsCount += groupRelationships.Count;
                }

                lock (_globalReferences)
                {
                    _globalReferences.AddRange(groupReferences);
                }
            }
        }
        finally
        {
            foreach (var parser in activeParsers.Values)
            {
                parser.Dispose();
            }
            foreach (var language in activeLanguages.Values)
            {
                language.Dispose();
            }
        }

        // Extract and parse dependencies (C# ProjectReferences, PackageReferences, and NPM package.json)
        await ParseProjectDependenciesAsync(_absoluteWorkspacePath, $"project:{_absoluteWorkspacePath}:");

        return (_totalNodesCount, _totalRelsCount, _nodesByKind);
    }

    public async Task<(int NodesCount, int RelationshipsCount, Dictionary<string, int> NodesByKind)> IndexAsync()
    {
        // Start background graph persistence consumer task
        var consumerTask = Task.Run(async () =>
        {
            await foreach (var writeFunc in _sharedChannel.Reader.ReadAllAsync())
            {
                try
                {
                    await writeFunc();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[PersistenceConsumer] Error writing to database: {ex.Message}");
                }
            }
        });

        // 1. Clear root database indices (ensure they exist)
        await _dbClient.CreateIndicesAsync();

        // 2. Discover all project directories
        var projectDirs = FindProjectDirectories();

        if (projectDirs.Count > 1 || (projectDirs.Count == 1 && projectDirs[0] != _absoluteWorkspacePath))
        {
            Console.Error.WriteLine($"[WorkspaceParser] Multi-project workspace detected. Discovering {projectDirs.Count} projects...");
            foreach (var projectDir in projectDirs)
            {
                _excludedSubdirectories.Add(projectDir);
            }

            // Surgically clear database workspaces sequentially to avoid transaction lock contention
            if (_clear)
            {
                Console.Error.WriteLine("[WorkspaceParser] Clearing project workspaces sequentially to avoid database lock contention...");
                foreach (var projectDir in projectDirs)
                {
                    Console.Error.WriteLine($"[WorkspaceParser] Clearing previous project data for '{projectDir}'...");
                    await _dbClient.ClearWorkspaceAsync(projectDir);
                }
                Console.Error.WriteLine($"[WorkspaceParser] Clearing previous root workspace data for '{_absoluteWorkspacePath}'...");
                await _dbClient.ClearWorkspaceAsync(_absoluteWorkspacePath);
            }

            // Index all projects in parallel
            var projectTasks = projectDirs.Select(async projectDir =>
            {
                var projectParser = new WorkspaceParser(
                    projectDir,
                    _dbClient,
                    false, // Already cleared sequentially above
                    _sharedChannel,
                    _globalSymbols,
                    _globalReferences,
                    _globalProjectDependencies
                );
                return await projectParser.IndexProjectInternalAsync();
            }).ToList();

            var projectResults = await Task.WhenAll(projectTasks);

            // Index any residual files at the root level outside any project
            Console.Error.WriteLine("[WorkspaceParser] Ingesting root files outside of any detected project...");

            var folderName = Path.GetFileName(_absoluteWorkspacePath);
            if (string.IsNullOrEmpty(folderName)) folderName = _absoluteWorkspacePath;

            var workspaceNode = new Database.Node(
                _workspaceNodeId,
                OntologyConstants.NodeLabels.Workspace,
                new Dictionary<string, object>
                {
                    ["path"] = _absoluteWorkspacePath,
                    ["name"] = folderName
                }
            );
            await _sharedChannel.Writer.WriteAsync(() => _dbClient.UploadNodesAsync(new List<Database.Node> { workspaceNode }));

            Scan(_absoluteWorkspacePath, _workspaceNodeId, new HashSet<string>(), false);

            if (_structuralNodes.Count > 0)
            {
                await _sharedChannel.Writer.WriteAsync(() => _dbClient.UploadNodesAsync(_structuralNodes));
            }
            if (_structuralRelationships.Count > 0)
            {
                await _sharedChannel.Writer.WriteAsync(() => _dbClient.UploadRelationshipsAsync(_structuralRelationships));
            }

            _totalNodesCount += _structuralNodes.Count;
            _totalRelsCount += _structuralRelationships.Count;
            foreach (var node in _structuralNodes)
            {
                if (!_nodesByKind.ContainsKey(node.Kind)) _nodesByKind[node.Kind] = 0;
                _nodesByKind[node.Kind]++;
            }

            var activeLanguages = new Dictionary<string, Language>();
            var activeParsers = new Dictionary<string, global::TreeSitter.Parser>();

            try
            {
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

                        try
                        {
                            if (!activeLanguages.TryGetValue(langParser.LanguageName, out var language))
                            {
                                language = new Language(langParser.LanguageName);
                                activeLanguages[langParser.LanguageName] = language;
                            }

                            if (!activeParsers.TryGetValue(langParser.LanguageName, out var parser))
                            {
                                parser = new global::TreeSitter.Parser(language);
                                activeParsers[langParser.LanguageName] = parser;
                            }

                            var sourceText = File.ReadAllText(file);
                            using var tree = parser.Parse(sourceText);

                            if (tree == null || tree.RootNode == null) continue;

                            var ctx = new FileContext(_absoluteWorkspacePath, relativePath, sourceText, langParser);

                            var fileNodeId = $"file:{_absoluteWorkspacePath}:{relativePath}";
                            ctx.Nodes.Add(new Database.Node(
                                fileNodeId,
                                OntologyConstants.NodeLabels.File,
                                new Dictionary<string, object>
                                {
                                    ["path"] = Path.GetFileName(file),
                                    ["name"] = Path.GetFileName(file)
                                }
                            ));

                            var parentDir = Path.GetDirectoryName(file)!.Replace('\\', '/');
                            var parentRelative = Path.GetRelativePath(_absoluteWorkspacePath, parentDir).Replace('\\', '/');
                            if (parentRelative == ".") parentRelative = "";

                            string parentNodeId = _workspaceNodeId;
                            if (_visitedDirs.TryGetValue(parentRelative, out var parentInfo))
                            {
                                parentNodeId = parentInfo.Id;
                            }

                            groupRelationships.Add(new Database.Relationship(parentNodeId, fileNodeId, OntologyConstants.Relationships.Contains));

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

                    if (groupNodes.Count > 0)
                    {
                        var nodesToUpload = groupNodes;
                        await _sharedChannel.Writer.WriteAsync(() => _dbClient.UploadNodesAsync(nodesToUpload));
                        _totalNodesCount += groupNodes.Count;

                        foreach (var node in groupNodes)
                        {
                            if (!_nodesByKind.ContainsKey(node.Kind)) _nodesByKind[node.Kind] = 0;
                            _nodesByKind[node.Kind]++;

                            if (node.Kind == OntologyConstants.NodeLabels.Class || node.Kind == OntologyConstants.NodeLabels.Interface || node.Kind == OntologyConstants.NodeLabels.Function)
                            {
                                if (node.Properties.TryGetValue("name", out var nameVal) && nameVal is string nameStr)
                                {
                                    lock (_globalSymbols)
                                    {
                                        _globalSymbols[(node.Kind, nameStr)] = node.Id;
                                    }
                                }
                            }
                        }
                    }

                    if (groupRelationships.Count > 0)
                    {
                        var relsToUpload = groupRelationships;
                        await _sharedChannel.Writer.WriteAsync(() => _dbClient.UploadRelationshipsAsync(relsToUpload));
                        _totalRelsCount += groupRelationships.Count;
                    }

                    lock (_globalReferences)
                    {
                        _globalReferences.AddRange(groupReferences);
                    }
                }
            }
            finally
            {
                foreach (var parser in activeParsers.Values)
                {
                    parser.Dispose();
                }
                foreach (var language in activeLanguages.Values)
                {
                    language.Dispose();
                }
            }

            // Aggregate all statistics from subprojects
            foreach (var result in projectResults)
            {
                _totalNodesCount += result.NodesCount;
                _totalRelsCount += result.RelationshipsCount;
                foreach (var kvp in result.NodesByKind)
                {
                    if (!_nodesByKind.ContainsKey(kvp.Key)) _nodesByKind[kvp.Key] = 0;
                    _nodesByKind[kvp.Key] += kvp.Value;
                }
            }
        }
        else
        {
            // Single project or root workspace is a project. Just run normally!
            Console.Error.WriteLine("[WorkspaceParser] Single project workspace detected.");
            await IndexProjectInternalAsync();
        }

        // Complete the persistence channel and await background writes
        _sharedChannel.Writer.Complete();
        await consumerTask;
        Console.Error.WriteLine("[WorkspaceParser] All background channel persistence writes completed successfully!");

        // Upload local cross-project dependencies (depends on) after all project nodes have been fully written
        if (_globalProjectDependencies.Count > 0)
        {
            Console.Error.WriteLine($"[WorkspaceParser] Uploading {_globalProjectDependencies.Count} local project dependency relationships...");
            await _dbClient.UploadRelationshipsAsync(_globalProjectDependencies);
            _totalRelsCount += _globalProjectDependencies.Count;
        }

        // 4. Deferred Global Reference Resolution & Final Reference Upload
        Console.Error.WriteLine($"[WorkspaceParser] Resolving {_globalReferences.Count} global cross-references...");
        var referenceRelationships = new List<Database.Relationship>();

        lock (_globalReferences)
        {
            foreach (var refItem in _globalReferences)
            {
                if (refItem.Kind == OntologyConstants.Relationships.Calls)
                {
                    lock (_globalSymbols)
                    {
                        if (_globalSymbols.TryGetValue((OntologyConstants.NodeLabels.Function, refItem.TargetName), out var targetNodeId))
                        {
                            referenceRelationships.Add(new Database.Relationship(refItem.ScopeSymbolId, targetNodeId, OntologyConstants.Relationships.Calls));
                        }
                    }
                }
                else if (refItem.Kind == OntologyConstants.Relationships.UsesType)
                {
                    lock (_globalSymbols)
                    {
                        if (_globalSymbols.TryGetValue((OntologyConstants.NodeLabels.Interface, refItem.TargetName), out var targetNodeId))
                        {
                            referenceRelationships.Add(new Database.Relationship(refItem.ScopeSymbolId, targetNodeId, OntologyConstants.Relationships.UsesType));
                        }
                        else if (_globalSymbols.TryGetValue((OntologyConstants.NodeLabels.Class, refItem.TargetName), out var targetClassId))
                        {
                            referenceRelationships.Add(new Database.Relationship(refItem.ScopeSymbolId, targetClassId, OntologyConstants.Relationships.UsesType));
                        }
                    }
                }
                else if (refItem.Kind == OntologyConstants.Relationships.Implements || refItem.Kind == OntologyConstants.Relationships.InheritsFrom)
                {
                    lock (_globalSymbols)
                    {
                        if (_globalSymbols.TryGetValue((OntologyConstants.NodeLabels.Interface, refItem.TargetName), out var targetNodeId))
                        {
                            referenceRelationships.Add(new Database.Relationship(refItem.ScopeSymbolId, targetNodeId, refItem.Kind));
                        }
                        else if (_globalSymbols.TryGetValue((OntologyConstants.NodeLabels.Class, refItem.TargetName), out var targetClassId))
                        {
                            referenceRelationships.Add(new Database.Relationship(refItem.ScopeSymbolId, targetClassId, refItem.Kind));
                        }
                    }
                }
                else if (refItem.Kind == OntologyConstants.Relationships.PotentialType)
                {
                    lock (_globalSymbols)
                    {
                        string? targetNodeId = null;
                        if (_globalSymbols.TryGetValue((OntologyConstants.NodeLabels.Interface, refItem.TargetName), out var targetIntfId))
                        {
                            targetNodeId = targetIntfId;
                        }
                        else if (_globalSymbols.TryGetValue((OntologyConstants.NodeLabels.Class, refItem.TargetName), out var targetClassId))
                        {
                            targetNodeId = targetClassId;
                        }

                        if (targetNodeId != null)
                        {
                            if (refItem.ScopeSymbolId != targetNodeId)
                            {
                                bool hasInheritance = referenceRelationships.Any(r =>
                                    r.From == refItem.ScopeSymbolId &&
                                    r.To == targetNodeId &&
                                    (r.Kind == OntologyConstants.Relationships.Implements || r.Kind == OntologyConstants.Relationships.InheritsFrom));

                                if (!hasInheritance)
                                {
                                    referenceRelationships.Add(new Database.Relationship(refItem.ScopeSymbolId, targetNodeId, OntologyConstants.Relationships.UsesType));
                                }
                            }
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
        string? kind = ctx.Parser.MapNodeType(node);
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
                ["file_path"] = Path.GetFileName(ctx.FilePath)
            };

            ctx.Nodes.Add(new Database.Node(symbolId, kind, properties));
            ctx.Relationships.Add(new Database.Relationship(parentId, symbolId, OntologyConstants.Relationships.Contains));
            currentParentId = symbolId;
        }

        // Collect references inside the current symbol scope
        if (currentParentId.StartsWith("symbol:"))
        {
            if (node.Type is "identifier" or "type_identifier")
            {
                ctx.References.Add(new Reference(currentParentId, node.Text, OntologyConstants.Relationships.PotentialType));
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
