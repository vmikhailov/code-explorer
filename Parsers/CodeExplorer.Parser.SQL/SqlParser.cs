using System.Text.RegularExpressions;
using CodeExplorer.Database;
using CodeExplorer.Common;
using Node = CodeExplorer.Database.Node;

namespace CodeExplorer.Parser;

public class SqlParser : IProjectParser, IFileParser
{
    public string LanguageName => "sql";

    public string ProjectType => "sql";

    public IReadOnlyCollection<string> ExcludedFolders => Array.Empty<string>();

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

    public async Task ParseCustomAsync(string filePath, string parentNodeId, ParsingContext ctx)
    {
        var relativePath = Path.GetRelativePath(ctx.AbsoluteWorkspacePath, filePath).Replace('\\', '/');
        var fileNodeId = $"file:{ctx.AbsoluteWorkspacePath}:{relativePath}";

        var fileNode = Node.FromNode(new FileNode(fileNodeId, Path.GetFileName(filePath), relativePath, filePath));
        await ctx.EnqueueUploadNodesAsync(new List<Node> { fileNode });
        ctx.IncrementNodeKind(OntologyConstants.NodeLabels.File);
        ctx.AddNodesCount(1);

        var fileRel = Relationship.FromRelationship(new ContainsRelationship(parentNodeId, fileNodeId));
        await ctx.EnqueueUploadRelationshipsAsync([fileRel]);
        ctx.AddRelsCount(1);

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

        var dbNode = Node.FromNode(new DbNode(dbNodeId, dbName, Path.GetRelativePath(ctx.AbsoluteWorkspacePath, filePath).Replace('\\', '/')));
        await ctx.EnqueueUploadNodesAsync(new List<Node> { dbNode });
        ctx.IncrementNodeKind(OntologyConstants.NodeLabels.DB);
        ctx.AddNodesCount(1);

        var dbRel = Relationship.FromRelationship(new UsesDbRelationship(parentNodeId, dbNodeId));
        await ctx.EnqueueUploadRelationshipsAsync(new List<Relationship> { dbRel });
        ctx.AddRelsCount(1);

        // 3. Identify Schema (DataSet)
        var schemaMatches = Regex.Matches(cleanSql, @"CREATE\s+SCHEMA\s+([a-zA-Z0-9_\[\]""#@]+)", RegexOptions.IgnoreCase);
        var datasets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in schemaMatches)
        {
            var schemaName = match.Groups[1].Value.Trim('[', ']', '"');
            var schemaNodeId = $"{dbNodeId}:dataset:{schemaName.ToLowerInvariant()}";
            datasets[schemaName] = schemaNodeId;

            var schemaNode = Node.FromNode(new DataSetNode(schemaNodeId, schemaName, Path.GetRelativePath(ctx.AbsoluteWorkspacePath, filePath).Replace('\\', '/')));
            await ctx.EnqueueUploadNodesAsync(new List<Node> { schemaNode });
            ctx.IncrementNodeKind(OntologyConstants.NodeLabels.DataSet);
            ctx.AddNodesCount(1);

            var schemaRel = Relationship.FromRelationship(new ContainsRelationship(dbNodeId, schemaNodeId));
            await ctx.EnqueueUploadRelationshipsAsync(new List<Relationship> { schemaRel });
            ctx.AddRelsCount(1);
        }

        // 4. Identify Tables
        var tableMatches = Regex.Matches(cleanSql, @"CREATE\s+TABLE\s+([a-zA-Z0-9_\.\[\]""#@]+)", RegexOptions.IgnoreCase);
        var tables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in tableMatches)
        {
            var rawTableName = match.Groups[1].Value;
            var parts = rawTableName.Split('.');
            string schemaName = "dbo";
            string tableName = rawTableName;
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
            if (!datasets.ContainsKey(schemaName))
            {
                datasets[schemaName] = schemaNodeId;
                var schemaNode = Node.FromNode(new DataSetNode(schemaNodeId, schemaName, Path.GetRelativePath(ctx.AbsoluteWorkspacePath, filePath).Replace('\\', '/')));
                await ctx.EnqueueUploadNodesAsync(new List<Node> { schemaNode });
                ctx.IncrementNodeKind(OntologyConstants.NodeLabels.DataSet);
                ctx.AddNodesCount(1);

                var schemaRel = Relationship.FromRelationship(new ContainsRelationship(dbNodeId, schemaNodeId));
                await ctx.EnqueueUploadRelationshipsAsync(new List<Relationship> { schemaRel });
                ctx.AddRelsCount(1);
            }

            var tableNodeId = $"{schemaNodeId}:table:{tableName.ToLowerInvariant()}";
            tables[tableName] = tableNodeId;
            tables[rawTableName] = tableNodeId;

            var tableNode = Node.FromNode(new TableNode(tableNodeId, tableName, Path.GetRelativePath(ctx.AbsoluteWorkspacePath, filePath).Replace('\\', '/')));
            await ctx.EnqueueUploadNodesAsync(new List<Node> { tableNode });
            ctx.IncrementNodeKind(OntologyConstants.NodeLabels.Table);
            ctx.AddNodesCount(1);

            var tableRel = Relationship.FromRelationship(new ContainsRelationship(schemaNodeId, tableNodeId));
            await ctx.EnqueueUploadRelationshipsAsync(new List<Relationship> { tableRel });
            ctx.AddRelsCount(1);

            // Register global table symbol
            ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Table, tableName, tableNodeId);
            if (parts.Length > 1)
            {
                ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Table, rawTableName, tableNodeId);
            }
        }

        // 5. Identify Procedures / Functions
        var procMatches = Regex.Matches(cleanSql, @"CREATE\s+(?:PROCEDURE|PROC|FUNCTION)\s+([a-zA-Z0-9_\.\[\]""#@]+)", RegexOptions.IgnoreCase);
        var procedures = new List<(string Name, string RawName, string Id, int StartIndex)>();

        for (int i = 0; i < procMatches.Count; i++)
        {
            var match = procMatches[i];
            var rawProcName = match.Groups[1].Value;
            var parts = rawProcName.Split('.');
            string schemaName = "dbo";
            string procName = rawProcName;
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
            if (!datasets.ContainsKey(schemaName))
            {
                datasets[schemaName] = schemaNodeId;
                var schemaNode = Node.FromNode(new DataSetNode(schemaNodeId, schemaName, Path.GetRelativePath(ctx.AbsoluteWorkspacePath, filePath).Replace('\\', '/')));
                await ctx.EnqueueUploadNodesAsync(new List<Node> { schemaNode });
                ctx.IncrementNodeKind(OntologyConstants.NodeLabels.DataSet);
                ctx.AddNodesCount(1);

                var schemaRel = Relationship.FromRelationship(new ContainsRelationship(dbNodeId, schemaNodeId));
                await ctx.EnqueueUploadRelationshipsAsync(new List<Relationship> { schemaRel });
                ctx.AddRelsCount(1);
            }

            var procNodeId = $"{schemaNodeId}:procedure:{procName.ToLowerInvariant()}";
            procedures.Add((procName, rawProcName, procNodeId, match.Index));

            var procNode = Node.FromNode(new ProcedureNode(procNodeId, procName, Path.GetRelativePath(ctx.AbsoluteWorkspacePath, filePath).Replace('\\', '/')));
            await ctx.EnqueueUploadNodesAsync(new List<Node> { procNode });
            ctx.IncrementNodeKind(OntologyConstants.NodeLabels.Procedure);
            ctx.AddNodesCount(1);

            var procRel = Relationship.FromRelationship(new ContainsRelationship(schemaNodeId, procNodeId));
            await ctx.EnqueueUploadRelationshipsAsync(new List<Relationship> { procRel });
            ctx.AddRelsCount(1);

            // Register global procedure symbol
            ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Procedure, procName, procNodeId);
            if (parts.Length > 1)
            {
                ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Procedure, rawProcName, procNodeId);
            }
        }

        // 6. Split statements and extract Query nodes (both inside procedures and top-level)
        var statements = cleanSql.Split(new[] { ';', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();

        // File node creation is managed above, so we just use the existing fileNodeId as the fallback parent
        
        int queryCounter = 0;
        int currentSearchIndex = 0;
        foreach (var statement in statements)
        {
            var firstWord = statement.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()?.ToUpperInvariant();

            if (firstWord is "SELECT" or "INSERT" or "UPDATE" or "DELETE" or "MERGE")
            {
                queryCounter++;
                var queryName = $"{firstWord} Query #{queryCounter}";
                
                // Find statement character index in cleanSql (searching forward to handle duplicates)
                var indexInCleanSql = cleanSql.IndexOf(statement, currentSearchIndex);
                if (indexInCleanSql != -1)
                {
                    currentSearchIndex = indexInCleanSql + statement.Length;
                }
                else
                {
                    indexInCleanSql = cleanSql.IndexOf(statement);
                }
                
                string containingParentId = fileNodeId;

                // Check if this query statement is enclosed in any procedure body
                for (int i = 0; i < procedures.Count; i++)
                {
                    var currentProc = procedures[i];
                    int start = currentProc.StartIndex;
                    
                    var nextGo = cleanSql.IndexOf("GO", start, StringComparison.OrdinalIgnoreCase);
                    int end = (nextGo != -1) ? nextGo : ((i + 1 < procedures.Count) ? procedures[i + 1].StartIndex : cleanSql.Length);

                    if (indexInCleanSql >= start && indexInCleanSql < end)
                    {
                        containingParentId = currentProc.Id;
                        break;
                    }
                }

                // Create the Query Node
                var queryNodeId = $"{containingParentId}:query:{queryCounter}";
                var queryNode = Node.FromNode(new QueryNode(
                    queryNodeId,
                    queryName,
                    statement.Length > 200 ? statement.Substring(0, 197) + "..." : statement,
                    Path.GetRelativePath(ctx.AbsoluteWorkspacePath, filePath).Replace('\\', '/')
                ));
                await ctx.EnqueueUploadNodesAsync(new List<Node> { queryNode });
                ctx.IncrementNodeKind(OntologyConstants.NodeLabels.Query);
                ctx.AddNodesCount(1);

                // Containment relation from Procedure or File
                var containmentRel = Relationship.FromRelationship(new ContainsRelationship(containingParentId, queryNodeId));
                await ctx.EnqueueUploadRelationshipsAsync(new List<Relationship> { containmentRel });
                ctx.AddRelsCount(1);

                // A. Parse Calls dependencies (EXEC / EXECUTE)
                var execMatches = Regex.Matches(statement, @"EXEC(?:UTE)?\s+([a-zA-Z0-9_\.\[\]""#@]+)", RegexOptions.IgnoreCase);
                foreach (Match execMatch in execMatches)
                {
                    var targetProcRaw = execMatch.Groups[1].Value;
                    var targetProcParts = targetProcRaw.Split('.');
                    var targetProcName = targetProcParts.Length > 1 ? targetProcParts[1].Trim('[', ']', '"') : targetProcRaw.Trim('[', ']', '"');
                    
                    var reference = new Reference(queryNodeId, targetProcName, OntologyConstants.Relationships.Calls);
                    ctx.AddGlobalReferences(new[] { reference });
                }

                // B. Parse Local Table dependencies (DependsOn)
                foreach (var tableKvp in tables)
                {
                    var tableName = tableKvp.Key;
                    var tableId = tableKvp.Value;

                    var pattern = $@"\b{Regex.Escape(tableName)}\b";
                    if (Regex.IsMatch(statement, pattern, RegexOptions.IgnoreCase))
                    {
                        var depRel = Relationship.FromRelationship(new DependsOnRelationship(queryNodeId, tableId));
                        await ctx.EnqueueUploadRelationshipsAsync(new List<Relationship> { depRel });
                        ctx.AddRelsCount(1);
                    }
                }

                // C. Parse potential external table dependencies for deferred global resolution
                var words = Regex.Matches(statement, @"\b[a-zA-Z0-9_]+\b");
                var uniqueWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (Match wm in words)
                {
                    uniqueWords.Add(wm.Value);
                }

                var tableReferences = new List<Reference>();
                foreach (var word in uniqueWords)
                {
                    if (!tables.ContainsKey(word))
                    {
                        tableReferences.Add(new Reference(queryNodeId, word, OntologyConstants.Relationships.DependsOn));
                    }
                }
                if (tableReferences.Count > 0)
                {
                    ctx.AddGlobalReferences(tableReferences);
                }
            }
        }
    }
}
