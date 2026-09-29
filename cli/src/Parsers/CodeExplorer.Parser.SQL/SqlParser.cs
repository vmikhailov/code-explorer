using System.Text.RegularExpressions;
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Parser;

[assembly: ParserAssembly]

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

    public IReadOnlyList<PackageDescriptor> Packages => [];
    public IReadOnlyList<ISemanticExtension> SemanticExtensions => [];

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".sql", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsProjectDirectory(string directoryPath, string[] filesInDirectory)
    {
        return false;
        //return filesInDirectory.Any(f => Path.GetExtension(f).Equals(".sql", StringComparison.OrdinalIgnoreCase));
    }

    public BaseParserVisitor CreateVisitor(
        TreeSitter.Node rootNode,
        List<ISemanticExtension> activeExtensions,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        SemanticExtensionRegistry extensionRegistry)
    {
        throw new NotSupportedException("SQL Parser does not use TreeSitter visitors.");
    }

    public ImportType ResolveImportType(string importPath, string filePath, string? absoluteWorkspacePath)
    {
        return ImportType.External;
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

        return new SyntaxTree(filePath, relativePath, null, null, null, fileNode, this, [], [], []);
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
        DatabaseNode? dbNode = null;
        string baseParentId = fileNodeId;

        if (dbMatch.Success)
        {
            var dbName = dbMatch.Groups[1].Value.Trim('[', ']', '"', '`');
            var dbNodeId = $"{workspaceId}:db:{dbName.ToLowerInvariant()}";
            dbNode = new DatabaseNode(dbNodeId, dbName, relativePath, "relational");
            fileNode.Children.Add(dbNode);
            baseParentId = dbNodeId;
        }

        // Helper to add nodes either to dbNode (if exists) or fileNode (if dbNode is null)
        void AddToParent(IOntologyNode child)
        {
            if (dbNode != null)
            {
                dbNode.Children.Add(child);
            }
            else
            {
                fileNode.Children.Add(child);
            }
        }

        // 3. Identify Schema (DataSet)
        var schemaMatches = Regex.Matches(cleanSql, 
            @"(?i)\bCREATE\s+SCHEMA\s+(?>(?:IF\s+NOT\s+EXISTS?\s+)?)(?<schemaName>[^\s\(;]+)");
        foreach (Match match in schemaMatches)
        {
            var rawSchemaName = match.Groups["schemaName"].Value;
            var schemaName = CleanIdentifier(rawSchemaName);
            if (string.IsNullOrWhiteSpace(schemaName) || IsDisallowedIdentifier(schemaName)) continue;

            var schemaNodeId = $"{baseParentId}:dataset:{schemaName.ToLowerInvariant()}";
            if (!datasets.TryGetValue(schemaName, out var schemaNode))
            {
                schemaNode = new DataSetNode(schemaNodeId, schemaName, relativePath);
                datasets[schemaName] = schemaNode;
                AddToParent(schemaNode);
            }
        }

        // 4. Identify Tables
        var tableMatches = Regex.Matches(cleanSql, 
            @"(?i)\bCREATE\s+(?:OR\s+REPLACE\s+)?(?:TEMPORARY\s+|TEMP\s+|EXTERNAL\s+)?TABLE\s+(?>(?:IF\s+NOT\s+EXISTS?\s+)?)(?<tableName>[^\s\(;]+)");
        foreach (Match match in tableMatches)
        {
            var rawTableName = match.Groups["tableName"].Value;
            var (schemaName, tableName) = ParseTableIdentifier(rawTableName);

            if (string.IsNullOrWhiteSpace(tableName) || IsDisallowedIdentifier(tableName))
            {
                continue;
            }

            var schemaNodeId = $"{baseParentId}:dataset:{schemaName.ToLowerInvariant()}";
            if (!datasets.TryGetValue(schemaName, out var schemaNode))
            {
                schemaNode = new DataSetNode(schemaNodeId, schemaName, relativePath);
                datasets[schemaName] = schemaNode;
                AddToParent(schemaNode);
            }

            var tableNodeId = $"{schemaNodeId}:table:{tableName.ToLowerInvariant()}";
            if (!tables.TryGetValue(tableName, out var tableNode))
            {
                tableNode = new TableNode(tableNodeId, tableName, relativePath);
                tables[tableName] = tableNode;
                schemaNode.Children.Add(tableNode);
            }
            tables[rawTableName] = tableNode;
        }

        // 5. Identify Procedures / Functions and their boundaries
        var procMatches = Regex.Matches(cleanSql, 
            @"(?i)\bCREATE\s+(?:OR\s+(?:REPLACE|ALTER)\s+)?(?:PROCEDURE|PROC|FUNCTION)\s+(?>(?:IF\s+NOT\s+EXISTS?\s+)?)(?<procName>[^\s\(;]+)");
        var tempScopes = new List<(Match Match, string Name, string RawName, string Id, ProcedureNode Node)>();
        for (var i = 0; i < procMatches.Count; i++)
        {
            var match = procMatches[i];
            var rawProcName = match.Groups["procName"].Value;
            var (schemaName, procName) = ParseTableIdentifier(rawProcName);

            if (string.IsNullOrWhiteSpace(procName) || IsDisallowedIdentifier(procName))
            {
                continue;
            }

            var schemaNodeId = $"{baseParentId}:dataset:{schemaName.ToLowerInvariant()}";
            if (!datasets.TryGetValue(schemaName, out var schemaNode))
            {
                schemaNode = new DataSetNode(schemaNodeId, schemaName, relativePath);
                datasets[schemaName] = schemaNode;
                AddToParent(schemaNode);
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

            var body = cleanSql[start..end];
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
                        statement.Length > 200 ? $"{statement[..197]}..." : statement,
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
            for (var idx = proc.StartIndex; idx < proc.EndIndex; idx++)
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
                    statement.Length > 200 ? $"{statement[..197]}..." : statement,
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
        var matchedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tableKvp in tables)
        {
            var tableName = tableKvp.Key;
            var pattern = $@"\b{Regex.Escape(tableName)}\b";
            if (Regex.IsMatch(statement, pattern, RegexOptions.IgnoreCase))
            {
                if (matchedTables.Add(tableName))
                {
                    queryNode.References.Add(new Reference(queryNodeId, tableName, OntologyConstants.Relationships.DependsOn));
                }
            }
        }

        // Match table references after FROM, JOIN, INTO, UPDATE, MERGE
        var fromMatches = Regex.Matches(statement, 
            @"(?i)\b(?:FROM|JOIN|INTO|UPDATE|MERGE)\s+(?!(?:EXTERNAL_QUERY|UNNEST|GENERATE_SERIES)\s*\()(?<target>[^\s\(;]+)");

        foreach (Match fm in fromMatches)
        {
            var raw = fm.Groups["target"].Value;
            var (_, tableName) = ParseTableIdentifier(raw);
            if (!string.IsNullOrWhiteSpace(tableName) && !IsDisallowedIdentifier(tableName) && matchedTables.Add(tableName))
            {
                queryNode.References.Add(new Reference(queryNodeId, tableName, OntologyConstants.Relationships.DependsOn));
            }
        }
    }

    private static readonly HashSet<string> DisallowedIdentifiers = new(StringComparer.OrdinalIgnoreCase)
    {
        "IF", "NOT", "EXISTS", "EXIST", "TABLE", "TEMP", "TEMPORARY", "EXTERNAL",
        "VIEW", "DATABASE", "SCHEMA", "PROCEDURE", "PROC", "FUNCTION", "TRIGGER", "INDEX",
        "SELECT", "INSERT", "UPDATE", "DELETE", "MERGE", "DROP", "ALTER", "CREATE",
        "OPTIONS", "PARTITION", "CLUSTER", "ORDER", "BY", "GROUP", "HAVING",
        "WHERE", "FROM", "JOIN", "INTO", "VALUES", "SET", "AS", "ON", "AND", "OR",
        "BEGIN", "END", "RETURN", "RETURNS", "DECLARE", "EXEC", "EXECUTE", "CALL",
        "EXTERNAL_QUERY", "UNNEST", "GENERATE_SERIES", "TABLE_FUNCTION", "NULL", "TRUE", "FALSE",
        "PRIMARY", "KEY", "FOREIGN", "REFERENCES", "CONSTRAINT", "DEFAULT", "CHECK", "UNIQUE",
        "INT", "INT64", "BIGINT", "STRING", "VARCHAR", "DATETIME", "TIMESTAMP", "FLOAT64", "FLOAT"
    };

    public static string CleanIdentifier(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        return raw.Trim().Trim('[', ']', '"', '`', '\'', '{', '}', '$', ' ', ',');
    }

    public static bool IsDisallowedIdentifier(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return true;
        var trimmed = CleanIdentifier(name);
        if (trimmed.Length <= 1) return true;
        if (char.IsDigit(trimmed[0])) return true;
        return DisallowedIdentifiers.Contains(trimmed);
    }

    public static (string schemaName, string tableName) ParseTableIdentifier(string rawTableName)
    {
        if (string.IsNullOrWhiteSpace(rawTableName))
        {
            return ("dbo", "");
        }

        var trimmed = rawTableName.Trim().TrimEnd(';');

        // If entire token is wrapped in ${...} or {{...}} or {...}
        if ((trimmed.StartsWith("${") && trimmed.EndsWith("}")) ||
            (trimmed.StartsWith("{{") && trimmed.EndsWith("}}")) ||
            (trimmed.StartsWith("{") && trimmed.EndsWith("}")))
        {
            var inner = trimmed.Trim('$', '{', '}').Trim();
            if (ConstantRegistry.TryResolve(null, inner, out var resolved))
            {
                trimmed = resolved;
            }
            else
            {
                trimmed = inner;
            }
        }

        var parts = trimmed.Split('.');
        string schemaName = "dbo";
        string tableName;

        if (parts.Length > 1)
        {
            schemaName = CleanIdentifier(parts[0]);
            tableName = CleanIdentifier(parts[1]);
        }
        else
        {
            tableName = CleanIdentifier(parts[0]);
        }

        if (string.IsNullOrWhiteSpace(schemaName) || IsDisallowedIdentifier(schemaName))
        {
            schemaName = "dbo";
        }

        return (schemaName, tableName);
    }

    public void CollectSemanticData(TreeSitter.Node node, string filePath, List<RawImport> rawImports, List<RawVariable> rawVariables, List<RawTypeBinding> rawTypeBindings)
    {
    }

    public ISyntaxEnricher GetSyntaxEnricher(SyntaxTree syntaxTree) => new SqlSyntaxEnricher(syntaxTree);
}
