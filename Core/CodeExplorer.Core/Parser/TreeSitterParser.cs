using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TreeSitter;

namespace CodeExplorer.Parser;

public class TreeSitterParser
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

    private static void ScanDirectory(
        string currentDir, 
        string absoluteWorkspacePath, 
        List<string> collectedFiles, 
        HashSet<string> detectedProjectTypes)
    {
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

        // 2. Scan current folder for project signature files to detect project types
        var filesInDir = Directory.GetFiles(currentDir);
        var newlyDetectedTypes = new HashSet<string>();

        foreach (var file in filesInDir)
        {
            var fileNameLower = Path.GetFileName(file).ToLowerInvariant();
            var ext = Path.GetExtension(file).ToLowerInvariant();

            if (ext == ".csproj" || ext == ".sln")
            {
                newlyDetectedTypes.Add("csharp");
            }
            else if (fileNameLower == "go.mod")
            {
                newlyDetectedTypes.Add("go");
            }
            else if (fileNameLower == "package.json" || fileNameLower == "tsconfig.json")
            {
                newlyDetectedTypes.Add("typescript");
            }
            else if (fileNameLower == "requirements.txt" || fileNameLower == "pyproject.toml" || fileNameLower == "setup.py")
            {
                newlyDetectedTypes.Add("python");
            }
        }

        // 3. Add to detected project types
        foreach (var type in newlyDetectedTypes)
        {
            detectedProjectTypes.Add(type);
        }

        // 4. Check if current directory name should be excluded based on the active/detected project types
        if (detectedProjectTypes.Contains("csharp") && (dirNameLower == "bin" || dirNameLower == "obj" || dirNameLower == ".vs"))
        {
            return;
        }
        if (detectedProjectTypes.Contains("typescript") && (dirNameLower == "node_modules" || dirNameLower == "dist" || dirNameLower == "build" || dirNameLower == ".next" || dirNameLower == "out"))
        {
            return;
        }
        if (detectedProjectTypes.Contains("go") && dirNameLower == "vendor")
        {
            return;
        }
        if (detectedProjectTypes.Contains("python") && (dirNameLower == "venv" || dirNameLower == ".venv" || dirNameLower == "__pycache__"))
        {
            return;
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
            ScanDirectory(subDir, absoluteWorkspacePath, collectedFiles, subProjectTypes);
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
        ScanDirectory(absoluteWorkspacePath, absoluteWorkspacePath, files, detectedProjectTypes);

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

                // Add Project Node based on top directory name
                var parts = relativePath.Split('/');
                var projectName = parts.Length > 1 ? parts[0] : "root";
                var projectId = $"project:{absoluteWorkspacePath}:{projectName}";

                // Ensure Project node is defined once globally
                if (!allNodes.Any(n => n.Id == projectId))
                {
                    allNodes.Add(new Database.Node(
                        projectId,
                        "Project",
                        new Dictionary<string, object> { ["name"] = projectName }
                    ));

                    // Relate Root contains Project
                    allRelationships.Add(new Database.Relationship(rootNodeId, projectId, "CONTAINS"));
                }

                // Relate Project contains File
                allRelationships.Add(new Database.Relationship(projectId, fileNodeId, "CONTAINS"));

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
