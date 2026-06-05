using System.Text.RegularExpressions;
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Parser;

namespace CodeExplorer.Parser.SQL;

public class SqlParser : IProjectParser, IFileParser
{
    private record ProcedureScope(
        string Name,
        string RawName,
        string Id,
        int StartIndex,
        int EndIndex,
        string Body,
        ProcedureNode Node
    );

    public string LanguageName => "sql";

    public string ProjectType => "sql";

    public IReadOnlyCollection<string> ExcludedFolders => [];

    public IReadOnlyList<ILibraryParser> LibraryParsers => [];

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".sql", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsProjectDirectory(string directoryPath, string[] filesInDirectory)
    {
        return false;
        //return filesInDirectory.Any(f => Path.GetExtension(f).Equals(".sql", StringComparison.OrdinalIgnoreCase));
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

    public async Task<SyntaxTree> ParseAsync(string filePath, string parentNodeId, string workspaceId, string absoluteWorkspacePath)
    {
        var relativePath = Path.GetRelativePath(absoluteWorkspacePath, filePath).Replace('\\', '/');
        var fileNodeId = $"{workspaceId}:file:{relativePath}";

        var fileNode = new FileNode(fileNodeId, Path.GetFileName(filePath), relativePath, filePath);
        var sqlText = await File.ReadAllTextAsync(filePath);

        // 1. Clean SQL comments to avoid false matches
        var withoutBlockComments = Regex.Replace(sqlText, @"/\*.*?\*/", "", RegexOptions.Singleline);
        var cleanSql = Regex.Replace(withoutBlockComments, @"--.*$", "", RegexOptions.Multiline);

        var datasets = new Dictionary<string, DataSetNode>(StringComparer.OrdinalIgnoreCase);
        var tables = new Dictionary<string, TableNode>(StringComparer.OrdinalIgnoreCase);
        var procedures = new List<ProcedureScope>();

        TryDetectContains(cleanSql, fileNode, fileNodeId, relativePath, datasets, tables, procedures, workspaceId);

        return new SyntaxTree(filePath, relativePath, null, null, null, fileNode, new List<RawImport>(), new List<RawVariable>());
    }

    private void TryDetectContains(
        string cleanSql,
        FileNode fileNode,
        string fileNodeId,
        string relativePath,
        Dictionary<string, DataSetNode> datasets,
        Dictionary<string, TableNode> tables,
        List<ProcedureScope> procedures,
        string workspaceId)
    {
        // 2. Identify Database (DB)
        var dbMatch = Regex.Match(cleanSql, @"CREATE\s+DATABASE\s+([a-zA-Z0-9_\[\]""#@`]+)", RegexOptions.IgnoreCase);
        string dbNodeId;
        string dbName;
        if (dbMatch.Success)
        {
            dbName = dbMatch.Groups[1].Value.Trim('[', ']', '"', '`');
            dbNodeId = $"{workspaceId}:db:{dbName.ToLowerInvariant()}";
        }
        else
        {
            var dirName = Path.GetFileName(Path.GetDirectoryName(fileNode.FullPath));
            if (string.IsNullOrEmpty(dirName)) dirName = "DefaultDB";
            dbName = dirName;
            dbNodeId = $"{workspaceId}:db:{dbName.ToLowerInvariant()}";
        }

        var dbNode = new DbNode(dbNodeId, dbName, relativePath);
        dbNode.SetExtension("db_type", "relational");
        fileNode.Children.Add(dbNode);

        // 3. Identify Schema (DataSet)
        var schemaMatches = Regex.Matches(cleanSql, @"CREATE\s+SCHEMA\s+(?:IF\s+NOT\s+EXISTS\s+)?([a-zA-Z0-9_\[\]""#@`]+)", RegexOptions.IgnoreCase);
        foreach (Match match in schemaMatches)
        {
            var schemaName = match.Groups[1].Value.Trim('[', ']', '"', '`');
            var schemaNodeId = $"{dbNodeId}:dataset:{schemaName.ToLowerInvariant()}";
            var schemaNode = new DataSetNode(schemaNodeId, schemaName, relativePath);
            datasets[schemaName] = schemaNode;
            dbNode.Children.Add(schemaNode);
        }

        // 4. Identify Tables
        var tableMatches = Regex.Matches(cleanSql, @"CREATE\s+TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?([a-zA-Z0-9_\.\[\]""#@`]+)", RegexOptions.IgnoreCase);
        foreach (Match match in tableMatches)
        {
            var rawTableName = match.Groups[1].Value;
            var parts = rawTableName.Split('.');
            var schemaName = "dbo";
            var tableName = rawTableName;
            if (parts.Length > 1)
            {
                schemaName = parts[0].Trim('[', ']', '"', '`');
                tableName = parts[1].Trim('[', ']', '"', '`');
            }
            else
            {
                tableName = rawTableName.Trim('[', ']', '"', '`');
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
        }

        // 5. Identify Procedures / Functions and their boundaries
        var procMatches = Regex.Matches(cleanSql, @"CREATE\s+(?:OR\s+(?:REPLACE|ALTER)\s+)?(?:PROCEDURE|PROC|FUNCTION)\s+([a-zA-Z0-9_\.\[\]""#@`]+)", RegexOptions.IgnoreCase);
        var tempScopes = new List<(Match Match, string Name, string RawName, string Id, ProcedureNode Node)>();
        for (var i = 0; i < procMatches.Count; i++)
        {
            var match = procMatches[i];
            var rawProcName = match.Groups[1].Value;
            var parts = rawProcName.Split('.');
            var schemaName = "dbo";
            var procName = rawProcName;
            if (parts.Length > 1)
            {
                schemaName = parts[0].Trim('[', ']', '"', '`');
                procName = parts[1].Trim('[', ']', '"', '`');
            }
            else
            {
                procName = rawProcName.Trim('[', ']', '"', '`');
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
            tempScopes.Add((match, procName, rawProcName, procNodeId, procNode));
            schemaNode.Children.Add(procNode);
        }

        // Resolve boundaries for each procedure body
        for (var i = 0; i < tempScopes.Count; i++)
        {
            var current = tempScopes[i];
            var start = current.Match.Index;
            var nextGo = cleanSql.IndexOf("GO", start, StringComparison.OrdinalIgnoreCase);
            var end = (nextGo != -1)
                ? nextGo
                : ((i + 1 < tempScopes.Count) ? tempScopes[i + 1].Match.Index : cleanSql.Length);

            var body = cleanSql.Substring(start, end - start);
            procedures.Add(new ProcedureScope(current.Name, current.RawName, current.Id, start, end, body, current.Node));
        }

        // 6. Nested Pass: Parse Queries inside Procedure Bodies
        var queryCounter = 0;
        foreach (var proc in procedures)
        {
            var procStatements = proc.Body.Split([';', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            foreach (var statement in procStatements)
            {
                var firstWord = statement.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault()?.ToUpperInvariant();

                if (firstWord is "SELECT" or "INSERT" or "UPDATE" or "DELETE" or "MERGE" or "EXEC" or "CALL")
                {
                    queryCounter++;
                    var queryName = $"{firstWord} Query #{queryCounter}";
                    var queryNodeId = $"{proc.Id}:query:{queryCounter}";
                    var queryNode = new QueryNode(
                        queryNodeId,
                        queryName,
                        statement.Length > 200 ? statement.Substring(0, 197) + "..." : statement,
                        relativePath
                    );
                    proc.Node.Children.Add(queryNode);

                    // Parse calls & table references inside this query statement
                    TryDetectCalls(statement, queryNode, queryNodeId);
                    TryDetectDependsOn(statement, queryNode, queryNodeId, tables);
                }
            }
        }

        // 7. Nested Pass: Parse Top-Level Queries (outside of procedures)
        // Mask out procedure bodies from cleanSql so we don't match query patterns inside them
        var charArray = cleanSql.ToCharArray();
        foreach (var proc in procedures)
        {
            for (int idx = proc.StartIndex; idx < proc.EndIndex; idx++)
            {
                charArray[idx] = ' ';
            }
        }
        var topLevelSql = new string(charArray);

        var topLevelStatements = topLevelSql.Split([';', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();

        foreach (var statement in topLevelStatements)
        {
            var firstWord = statement.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()?.ToUpperInvariant();

            if (firstWord is "SELECT" or "INSERT" or "UPDATE" or "DELETE" or "MERGE" or "EXEC" or "CALL")
            {
                queryCounter++;
                var queryName = $"{firstWord} Query #{queryCounter}";
                var queryNodeId = $"{fileNodeId}:query:{queryCounter}";
                var queryNode = new QueryNode(
                    queryNodeId,
                    queryName,
                    statement.Length > 200 ? statement.Substring(0, 197) + "..." : statement,
                    relativePath
                );
                fileNode.Children.Add(queryNode);

                // Parse calls & table references inside this top-level query statement
                TryDetectCalls(statement, queryNode, queryNodeId);
                TryDetectDependsOn(statement, queryNode, queryNodeId, tables);
            }
        }
    }

    private void TryDetectCalls(string statement, QueryNode queryNode, string queryNodeId)
    {
        var execMatches = Regex.Matches(statement, @"EXEC(?:UTE)?\s+([a-zA-Z0-9_\.\[\]""#@`]+)", RegexOptions.IgnoreCase);
        foreach (Match execMatch in execMatches)
        {
            var targetProcRaw = execMatch.Groups[1].Value;
            var targetProcParts = targetProcRaw.Split('.');
            var targetProcName = targetProcParts.Length > 1 ? targetProcParts[1].Trim('[', ']', '"', '`') : targetProcRaw.Trim('[', ']', '"', '`');

            queryNode.References.Add(new Reference(queryNodeId, targetProcName, OntologyConstants.Relationships.Calls));
        }
    }

    private void TryDetectDependsOn(string statement, QueryNode queryNode, string queryNodeId, Dictionary<string, TableNode> tables)
    {
        foreach (var tableKvp in tables)
        {
            var tableName = tableKvp.Key;
            var pattern = $@"\b{Regex.Escape(tableName)}\b";
            if (Regex.IsMatch(statement, pattern, RegexOptions.IgnoreCase))
            {
                queryNode.References.Add(new Reference(queryNodeId, tableName, OntologyConstants.Relationships.DependsOn));
            }
        }

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

    public void CollectSemanticData(TreeSitter.Node node, string filePath, List<RawImport> rawImports, List<RawVariable> rawVariables)
    {
    }

    public ISemanticModel GetSemanticModel(SyntaxTree syntaxTree) => new SqlSemanticModel(syntaxTree);
}
