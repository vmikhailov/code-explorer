using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        List<string> collectedFiles,
        HashSet<string> detectedProjectTypes,
        Dictionary<string, (string Id, string Kind)> visitedDirs,
        List<Database.Node> allNodes,
        List<Database.Relationship> allRelationships,
        bool insideProject)
    {
        var relativeDir = Path.GetRelativePath(absoluteWorkspacePath, currentDir).Replace('\\', '/');
        if (relativeDir == ".") relativeDir = "";

        var dirName = Path.GetFileName(currentDir);
        if (string.IsNullOrEmpty(dirName))
        {
            dirName = currentDir;
        }
        var dirNameLower = dirName.ToLowerInvariant();

        // 1. Generic default exclusions
        var genericExclusions = new HashSet<string> { ".git", ".github", ".vscode", ".idea" };
        if (genericExclusions.Contains(dirNameLower))
        {
            return;
        }

        // 2. Scan current folder for project signatures by querying registered language parsers
        var filesInDir = Directory.GetFiles(currentDir);
        var newlyDetectedTypes = new HashSet<string>();
        bool isProject = false;
        string? projectType = null;

        lock (Parsers)
        {
            foreach (var parser in Parsers)
            {
                if (parser.IsProjectDirectory(currentDir, filesInDir))
                {
                    newlyDetectedTypes.Add(parser.ProjectType);
                    isProject = true;
                    projectType = parser.ProjectType;
                }
            }
        }

        // A project cannot contain another project.
        if (isProject && insideProject)
        {
            isProject = false;
        }

        // Propagate whether we are inside a project to subdirectories
        bool currentInsideProject = insideProject || isProject;

        // 3. Add to detected project types
        foreach (var type in newlyDetectedTypes)
        {
            detectedProjectTypes.Add(type);
        }

        // 4. Check if current directory name should be excluded based on active project types and language exclusions
        bool shouldExclude = false;
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
                            break;
                        }
                    }
                }
                if (shouldExclude) break;
            }
        }

        if (shouldExclude)
        {
            return;
        }

        // Register current directory in visitedDirs and allNodes
        string currentId;
        string currentKind;

        if (string.IsNullOrEmpty(relativeDir))
        {
            // Root directory
            currentId = $"root:{absoluteWorkspacePath}";
            currentKind = "Root";
            visitedDirs[relativeDir] = (currentId, currentKind);
        }
        else
        {
            if (isProject)
            {
                currentId = $"project:{absoluteWorkspacePath}:{relativeDir}";
                currentKind = "Project";
                allNodes.Add(new Database.Node(currentId, "Project", new Dictionary<string, object>
                {
                    ["name"] = dirName,
                    ["path"] = relativeDir,
                    ["project_type"] = projectType ?? "unknown"
                }));
            }
            else
            {
                currentId = $"folder:{absoluteWorkspacePath}:{relativeDir}";
                currentKind = "Folder";
                allNodes.Add(new Database.Node(currentId, "Folder", new Dictionary<string, object>
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
                allRelationships.Add(new Database.Relationship(parentInfo.Id, currentId, "CONTAINS"));
            }
        }

        // Add matching source files in this folder
        foreach (var file in filesInDir)
        {
            var ext = Path.GetExtension(file).ToLowerInvariant();
            lock (Parsers)
            {
                if (Parsers.Any(p => p.CanParse(ext)))
                {
                    collectedFiles.Add(file);
                }
            }
        }

        // Recursively traverse subdirectories
        var subDirs = Directory.GetDirectories(currentDir);
        foreach (var subDir in subDirs)
        {
            // We pass a copy of detectedProjectTypes so that subdirectories inherit active project types
            var subProjectTypes = new HashSet<string>(detectedProjectTypes);
            Scan(subDir, absoluteWorkspacePath, collectedFiles, subProjectTypes, visitedDirs, allNodes, allRelationships, currentInsideProject);
        }
    }

    public static (List<Database.Node> Nodes, List<Database.Relationship> Relationships) ParseDirectory(string dirPath)
    {
        var allNodes = new List<Database.Node>();
        var allRelationships = new List<Database.Relationship>();
        var allReferences = new List<Reference>();

        var absoluteWorkspacePath = Path.GetFullPath(dirPath).Replace('\\', '/');
        var folderName = Path.GetFileName(absoluteWorkspacePath);
        if (string.IsNullOrEmpty(folderName)) folderName = absoluteWorkspacePath;
        var rootNodeId = $"root:{absoluteWorkspacePath}";

        allNodes.Add(new Database.Node(
            rootNodeId,
            "Root",
            new Dictionary<string, object>
            {
                ["path"] = absoluteWorkspacePath,
                ["name"] = folderName
            }
        ));

        // Use custom directory scanning that detects project types and handles exclusions
        var files = new List<string>();
        var detectedProjectTypes = new HashSet<string>();
        var visitedDirs = new Dictionary<string, (string Id, string Kind)>();
        Scan(absoluteWorkspacePath, absoluteWorkspacePath, files, detectedProjectTypes, visitedDirs, allNodes, allRelationships, false);

        foreach (var file in files)
        {
            var ext = Path.GetExtension(file).ToLower();

            ILanguageParser? langParser = null;
            lock (Parsers)
            {
                langParser = Parsers.FirstOrDefault(p => p.CanParse(ext));
            }
            if (langParser == null) continue;

            try
            {
                using var language = new Language(langParser.LanguageName);
                using var parser = new global::TreeSitter.Parser(language);

                var sourceText = File.ReadAllText(file);
                using var tree = parser.Parse(sourceText);

                if (tree == null || tree.RootNode == null) continue;

                var relativePath = Path.GetRelativePath(dirPath, file).Replace('\\', '/');
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

                string parentNodeId = rootNodeId;
                if (visitedDirs.TryGetValue(parentRelative, out var parentInfo))
                {
                    parentNodeId = parentInfo.Id;
                }

                // Relate Parent Node (Folder, Project, or Root) contains File
                allRelationships.Add(new Database.Relationship(parentNodeId, fileNodeId, "CONTAINS"));

                // Traverse AST
                TraverseNode(tree.RootNode, fileNodeId, ctx);

                allNodes.AddRange(ctx.Nodes);
                allRelationships.AddRange(ctx.Relationships);
                allReferences.AddRange(ctx.References);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error parsing file {file}: {ex.Message}");
            }
        }

        // Perform global semantic resolution
        var definedSymbolsByKindAndName = allNodes
            .Where(n => n.Kind is "Class" or "Function")
            .GroupBy(n => (n.Kind, Name: (string)n.Properties["name"]))
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var refItem in allReferences)
        {
            if (refItem.Kind == "CALLS")
            {
                if (definedSymbolsByKindAndName.TryGetValue(("Function", refItem.TargetName), out var targetNode))
                {
                    allRelationships.Add(new Database.Relationship(refItem.ScopeSymbolId, targetNode.Id, "CALLS"));
                }
            }
            else if (refItem.Kind == "USES_TYPE")
            {
                if (definedSymbolsByKindAndName.TryGetValue(("Class", refItem.TargetName), out var targetNode))
                {
                    allRelationships.Add(new Database.Relationship(refItem.ScopeSymbolId, targetNode.Id, "USES_TYPE"));
                }
            }
            else if (refItem.Kind == "IMPLEMENTS" || refItem.Kind == "INHERITS_FROM")
            {
                if (definedSymbolsByKindAndName.TryGetValue(("Class", refItem.TargetName), out var targetNode))
                {
                    allRelationships.Add(new Database.Relationship(refItem.ScopeSymbolId, targetNode.Id, refItem.Kind));
                }
            }
            else if (refItem.Kind == "POTENTIAL_TYPE")
            {
                if (definedSymbolsByKindAndName.TryGetValue(("Class", refItem.TargetName), out var targetNode))
                {
                    if (refItem.ScopeSymbolId != targetNode.Id)
                    {
                        bool hasInheritance = allRelationships.Any(r =>
                            r.From == refItem.ScopeSymbolId &&
                            r.To == targetNode.Id &&
                            (r.Kind == "IMPLEMENTS" || r.Kind == "INHERITS_FROM"));

                        if (!hasInheritance)
                        {
                            allRelationships.Add(new Database.Relationship(refItem.ScopeSymbolId, targetNode.Id, "USES_TYPE"));
                        }
                    }
                }
            }
        }

        return (allNodes, allRelationships);
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
}
