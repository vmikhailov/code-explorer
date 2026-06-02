using System.Text.RegularExpressions;
using CodeExplorer.Database;
using CodeExplorer.Common;
using Node = CodeExplorer.Database.Node;

namespace CodeExplorer.Parser;

public class SqlParser : IProjectParser, IFileParser
{
    public string LanguageName => "sql";

    public string ProjectType => "sql";

    public IReadOnlyCollection<string> ExcludedFolders => [];

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".sql", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsProjectDirectory(string directoryPath, string[] filesInDirectory)
    {
        return filesInDirectory.Any(f => Path.GetExtension(f).Equals(".sql", StringComparison.OrdinalIgnoreCase));
    }

    public string? MapNodeType(TreeSitter.Node node)
    {
        // Custom parser does not use TreeSitter Nodes
        return null;
    }

    public string? ExtractIdentifier(TreeSitter.Node node)
    {
        // Custom parser does not use TreeSitter Nodes
        return null;
    }

    public void CollectReferences(TreeSitter.Node node, string scopeSymbolId, List<Reference> references)
    {
        // Custom parser does not use TreeSitter Nodes
    }

    public Task<ProducedPackageInfo?> GetProducedPackageAsync(string projectDirectory)
    {
        return Task.FromResult<ProducedPackageInfo?>(null);
    }

    public Task<ProjectDependencyInfo> ParseDependenciesAsync(string projectDirectory)
    {
        return Task.FromResult(new ProjectDependencyInfo(new List<string>(), new List<ProducedPackageInfo>()));
    }

    public bool UsesTreeSitter => false;

    public async Task<FileNode> ParseAsync(string filePath, string parentNodeId, ParsingContext ctx)
    {
        var relativePath = Path.GetRelativePath(ctx.AbsoluteWorkspacePath, filePath).Replace('\\', '/');
        var fileNodeId = $"file:{ctx.AbsoluteWorkspacePath}:{relativePath}";

        var fileNode = new FileNode(fileNodeId, Path.GetFileName(filePath), relativePath, filePath);
        var sqlText = await File.ReadAllTextAsync(filePath);

        // 1. Clean SQL comments to avoid false matches
        var withoutBlockComments = Regex.Replace(sqlText, @"/\*.*?\*/", "", RegexOptions.Singleline);
        var cleanSql = Regex.Replace(withoutBlockComments, @"--.*$", "", RegexOptions.Multiline);

        // 2. Identify Database (DB)
        var dbMatch = Regex.Match(cleanSql, @"CREATE\s+DATABASE\s+([a-zA-Z0-9_\[\]""#@]+)", RegexOptions.IgnoreCase);
        string dbNodeId;
        string dbName;
        if (dbMatch.Success)
        {
            dbName = dbMatch.Groups[1].Value.Trim('[', ']', '"');
            dbNodeId = $"db:{dbName.ToLowerInvariant()}";
        }
        else
        {
            var dirName = Path.GetFileName(Path.GetDirectoryName(filePath));
            if (string.IsNullOrEmpty(dirName)) dirName = "DefaultDB";
            dbName = dirName;
            dbNodeId = $"db:{dbName.ToLowerInvariant()}";
        }

        var dbNode = new DbNode(dbNodeId, dbName, relativePath);
        fileNode.Children.Add(dbNode);

        // 3. Identify Schema (DataSet)
        var schemaMatches = Regex.Matches(cleanSql, @"CREATE\s+SCHEMA\s+([a-zA-Z0-9_\[\]""#@]+)", RegexOptions.IgnoreCase);
        var datasets = new Dictionary<string, DataSetNode>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in schemaMatches)
        {
            var schemaName = match.Groups[1].Value.Trim('[', ']', '"');
            var schemaNodeId = $"{dbNodeId}:dataset:{schemaName.ToLowerInvariant()}";
            var schemaNode = new DataSetNode(schemaNodeId, schemaName, relativePath);
            datasets[schemaName] = schemaNode;
            dbNode.Children.Add(schemaNode);
        }

        // 4. Identify Tables
        var tableMatches = Regex.Matches(cleanSql, @"CREATE\s+TABLE\s+([a-zA-Z0-9_\.\[\]""#@]+)", RegexOptions.IgnoreCase);
        var tables = new Dictionary<string, TableNode>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in tableMatches)
        {
            var rawTableName = match.Groups[1].Value;
            var parts = rawTableName.Split('.');
            var schemaName = "dbo";
            var tableName = rawTableName;
            if (parts.Length > 1)
            {
                schemaName = parts[0].Trim('[', ']', '"');
                tableName = parts[1].Trim('[', ']', '"');
            }
            else
            {
                tableName = rawTableName.Trim('[', ']', '"');
            }

            var schemaNodeId = $"{dbNodeId}:dataset:{schemaName.ToLowerInvariant()}";
            if (!datasets.TryGetValue(schemaName, out var schemaNode))
            {
                schemaNode = new DataSetNode(schemaNodeId, schemaName, relativePath);
                datasets[schemaName] = schemaNode;
                dbNode.Children.Add(schemaNode);
            }

            var tableNodeId = $"{schemaNodeId}:table:{tableName.ToLowerInvariant()}";
            var tableNode = new TableNode(tableNodeId, tableName, relativePath);
            tables[tableName] = tableNode;
            tables[rawTableName] = tableNode;
            schemaNode.Children.Add(tableNode);

            // Register global table symbol
            ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Table, tableName, tableNodeId);
            if (parts.Length > 1)
            {
                ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Table, rawTableName, tableNodeId);
            }
        }

        // 5. Identify Procedures / Functions
        var procMatches = Regex.Matches(cleanSql, @"CREATE\s+(?:PROCEDURE|PROC|FUNCTION)\s+([a-zA-Z0-9_\.\[\]""#@]+)", RegexOptions.IgnoreCase);
        var procedures = new List<(string Name, string RawName, string Id, int StartIndex, ProcedureNode Node)>();

        for (var i = 0; i < procMatches.Count; i++)
        {
            var match = procMatches[i];
            var rawProcName = match.Groups[1].Value;
            var parts = rawProcName.Split('.');
            var schemaName = "dbo";
            var procName = rawProcName;
            if (parts.Length > 1)
            {
                schemaName = parts[0].Trim('[', ']', '"');
                procName = parts[1].Trim('[', ']', '"');
            }
            else
            {
                procName = rawProcName.Trim('[', ']', '"');
            }

            var schemaNodeId = $"{dbNodeId}:dataset:{schemaName.ToLowerInvariant()}";
            if (!datasets.TryGetValue(schemaName, out var schemaNode))
            {
                schemaNode = new DataSetNode(schemaNodeId, schemaName, relativePath);
                datasets[schemaName] = schemaNode;
                dbNode.Children.Add(schemaNode);
            }

            var procNodeId = $"{schemaNodeId}:procedure:{procName.ToLowerInvariant()}";
            var procNode = new ProcedureNode(procNodeId, procName, relativePath);
            procedures.Add((procName, rawProcName, procNodeId, match.Index, procNode));
            schemaNode.Children.Add(procNode);

            // Register global procedure symbol
            ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Procedure, procName, procNodeId);
            if (parts.Length > 1)
            {
                ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Procedure, rawProcName, procNodeId);
            }
        }

        // 6. Split statements and extract Query nodes (both inside procedures and top-level)
        var statements = cleanSql.Split([';', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();

        var queryCounter = 0;
        var currentSearchIndex = 0;
        foreach (var statement in statements)
        {
            var firstWord = statement.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()?.ToUpperInvariant();

            if (firstWord is "SELECT" or "INSERT" or "UPDATE" or "DELETE" or "MERGE")
            {
                queryCounter++;
                var queryName = $"{firstWord} Query #{queryCounter}";
                
                var indexInCleanSql = cleanSql.IndexOf(statement, currentSearchIndex);
                if (indexInCleanSql != -1)
                {
                    currentSearchIndex = indexInCleanSql + statement.Length;
                }
                else
                {
                    indexInCleanSql = cleanSql.IndexOf(statement);
                }
                
                IOntologyNode containingParentNode = fileNode;
                var containingParentId = fileNodeId;

                // Check if this query statement is enclosed in any procedure body
                for (var i = 0; i < procedures.Count; i++)
                {
                    var currentProc = procedures[i];
                    var start = currentProc.StartIndex;
                    
                    var nextGo = cleanSql.IndexOf("GO", start, StringComparison.OrdinalIgnoreCase);
                    var end = (nextGo != -1) ? nextGo : ((i + 1 < procedures.Count) ? procedures[i + 1].StartIndex : cleanSql.Length);

                    if (indexInCleanSql >= start && indexInCleanSql < end)
                    {
                        containingParentNode = currentProc.Node;
                        containingParentId = currentProc.Id;
                        break;
                    }
                }

                // Create the Query Node
                var queryNodeId = $"{containingParentId}:query:{queryCounter}";
                var queryNode = new QueryNode(
                    queryNodeId,
                    queryName,
                    statement.Length > 200 ? statement.Substring(0, 197) + "..." : statement,
                    relativePath
                );
                containingParentNode.Children.Add(queryNode);

                // A. Parse Calls dependencies (EXEC / EXECUTE)
                var execMatches = Regex.Matches(statement, @"EXEC(?:UTE)?\s+([a-zA-Z0-9_\.\[\]""#@]+)", RegexOptions.IgnoreCase);
                foreach (Match execMatch in execMatches)
                {
                    var targetProcRaw = execMatch.Groups[1].Value;
                    var targetProcParts = targetProcRaw.Split('.');
                    var targetProcName = targetProcParts.Length > 1 ? targetProcParts[1].Trim('[', ']', '"') : targetProcRaw.Trim('[', ']', '"');
                    
                    queryNode.References.Add(new Reference(queryNodeId, targetProcName, OntologyConstants.Relationships.Calls));
                }

                // B. Parse Local Table dependencies (DependsOn)
                foreach (var tableKvp in tables)
                {
                    var tableName = tableKvp.Key;
                    var tableNode = tableKvp.Value;

                    var pattern = $@"\b{Regex.Escape(tableName)}\b";
                    if (Regex.IsMatch(statement, pattern, RegexOptions.IgnoreCase))
                    {
                        queryNode.References.Add(new Reference(queryNodeId, tableName, OntologyConstants.Relationships.DependsOn));
                    }
                }

                // C. Parse potential external table dependencies for deferred global resolution
                var words = Regex.Matches(statement, @"\b[a-zA-Z0-9_]+\b");
                var uniqueWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (Match wm in words)
                {
                    uniqueWords.Add(wm.Value);
                }

                foreach (var word in uniqueWords)
                {
                    if (!tables.ContainsKey(word))
                    {
                        queryNode.References.Add(new Reference(queryNodeId, word, OntologyConstants.Relationships.DependsOn));
                    }
                }
            }
        }

        return fileNode;
    }
}
