using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes;

namespace CodeExplorer.Core.Parser;

public abstract class BaseSemanticAnalyzer : ISemanticAnalyzer
{
    protected abstract IReadOnlyDictionary<string, HashSet<string>> DbPackages { get; }

    protected virtual bool IsDbPackage(string importPath)
    {
        var clean = Path.GetFileName(importPath);
        return DbPackages.Values.Any(set => set.Contains(importPath) || set.Contains(clean));
    }

    protected virtual string GetDbType(string importPath)
    {
        var clean = Path.GetFileName(importPath);
        foreach (var kvp in DbPackages)
        {
            if (kvp.Value.Contains(importPath) || kvp.Value.Contains(clean))
            {
                return kvp.Key;
            }
        }
        return "unknown";
    }

    protected abstract bool IsApiPackage(string importPath);
    protected virtual bool IsCloudPackage(string importPath) => false;

    protected static readonly Regex ConfigRegex = new(
        @"(?i)(config|settings?|cfg|\benv\b|db_?conn|\burl\b|\buri\b|\bport\b|\bhost\b|user(name)?|pass(word)?|token|secret|\bkey\b|auth|api_?key|connection_?string)",
        RegexOptions.Compiled
    );

    protected static readonly Regex ConfigInitializerRegex = new(
        @"(?i)(process\.env|Configuration\[|Environment\.GetEnvironmentVariable|System\.Environment|import\.meta\.env|dotenv|require\(['""]dotenv['""]\))",
        RegexOptions.Compiled
    );

    protected static readonly Regex EtlRegex = new(
        @"(?i)(\betl\b|\bsql\b|\bquery\b|\bselect\b|\binsert\b|\bupsert\b|\bschema\b|\btable\b|\bcolumn\b|\bdatabase\b|\bmigration\b|\bextract\b|\btransform\b|\bload\b)",
        RegexOptions.Compiled
    );

    protected static readonly Regex SqlQueryRegex = new(
        @"(?i)^[\s@$""'\`]*\s*(select|insert|update|delete|create\s+table|drop\s+table|merge|alter\s+table)\b",
        RegexOptions.Compiled
    );

    public virtual async Task AnalyzeAndEnrichAsync(ProjectNode projectNode, ParsingContext ctx)
    {
        var files = new List<FileNode>();
        FindAllFiles(projectNode, files);

        var internalPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var child in projectNode.Children.OfType<PackageNode>())
        {
            internalPackages.Add(child.Name);
        }

        foreach (var file in files)
        {
            var relativePath = file.Path;
            var fileImports = ctx.RawImports.Where(i => i.FilePath == relativePath).ToList();

            var usesDb = fileImports.Any(i => IsDbPackage(i.Path));
            var usesApi = fileImports.Any(i => IsApiPackage(i.Path));
            var usesCloud = fileImports.Any(i => IsCloudPackage(i.Path));

            if (usesDb)
            {
                file.SetExtension("uses_database", "true");
                var firstDbImport = fileImports.FirstOrDefault(i => IsDbPackage(i.Path));
                if (firstDbImport != null)
                {
                    var dbEngine = MapPackageToDbEngine(firstDbImport.Path);
                    var dbType = GetDbType(firstDbImport.Path);
                    var dbId = $"{ctx.WorkspaceId}:db:{dbEngine.ToLowerInvariant()}";
                    var dbNode = new DbNode(dbId, dbEngine, dbId);
                    dbNode.SetExtension("db_type", dbType);
                    file.Children.Add(dbNode);
                }
            }

            if (usesApi)
            {
                file.SetExtension("uses_api", "true");
            }

            if (usesCloud)
            {
                file.SetExtension("uses_cloud", "true");
                var firstCloudImport = fileImports.FirstOrDefault(i => IsCloudPackage(i.Path));
                if (firstCloudImport != null)
                {
                    var cloudService = MapPackageToCloudService(firstCloudImport.Path);
                    var cloudId = $"{ctx.WorkspaceId}:cloud:{cloudService.ToLowerInvariant()}";
                    var cloudNode = new CloudServiceNode(cloudId, cloudService, "CloudService", cloudId);
                    file.Children.Add(cloudNode);
                }
            }

            foreach (var child in projectNode.Children.OfType<PackageNode>())
            {
                var isInternal = internalPackages.Contains(child.Name);
                child.SetExtension("is_external", isInternal ? "false" : "true");
            }

            var fileVariables = ctx.RawVariables.Where(v => v.FilePath == relativePath).ToList();
            foreach (var rawVar in fileVariables)
            {
                var isConfig = ConfigRegex.IsMatch(rawVar.Name) || ConfigInitializerRegex.IsMatch(rawVar.InitializerText);
                var isEtl = EtlRegex.IsMatch(rawVar.Name) || SqlQueryRegex.IsMatch(rawVar.InitializerText);
                var isConstant = rawVar.IsConstant;
                var isGlobal = rawVar.Scope == "global";

                if (isConfig || isEtl || isConstant || isGlobal)
                {
                    var varTypeStr = new List<string>();
                    if (isConfig) varTypeStr.Add("config");
                    if (isEtl) varTypeStr.Add("etl");
                    if (isConstant) varTypeStr.Add("constant");
                    if (isGlobal) varTypeStr.Add("global");

                    var varType = string.Join(",", varTypeStr);
                    var varId = $"{ctx.WorkspaceId}:symbol:{relativePath}:Variable:{rawVar.Name}:{rawVar.StartLine}";

                    var ext = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["variable_type"] = varType,
                        ["initializer_expression"] = rawVar.InitializerText,
                        ["is_constant"] = isConstant ? "true" : "false"
                    };

                    var varNode = new VariableNode(
                        varId,
                        rawVar.Name,
                        varId,
                        file.FullPath,
                        varId,
                        rawVar.StartLine,
                        rawVar.EndLine,
                        rawVar.StartCol,
                        rawVar.EndCol,
                        ext
                    );

                    TryInsertVariable(file, varNode, rawVar.StartLine);
                }
            }
        }
        await Task.CompletedTask;
    }

    private static void FindAllFiles(IOntologyNode node, List<FileNode> files)
    {
        if (node is FileNode f)
        {
            files.Add(f);
        }
        foreach (var child in node.Children)
        {
            FindAllFiles(child, files);
        }
    }

    private static bool TryInsertVariable(IOntologyNode parentNode, VariableNode varNode, int line)
    {
        foreach (var child in parentNode.Children)
        {
            if (child is ClassNode cn && line >= cn.StartLine && line <= cn.EndLine)
            {
                if (TryInsertVariable(cn, varNode, line)) return true;
            }
            if (child is FunctionNode fn && line >= fn.StartLine && line <= fn.EndLine)
            {
                if (TryInsertVariable(fn, varNode, line)) return true;
            }
        }

        parentNode.Children.Add(varNode);
        return true;
    }

    protected virtual string MapPackageToDbEngine(string packageName)
    {
        var lower = packageName.ToLowerInvariant();
        if (lower.Contains("pg") || lower.Contains("npgsql") || lower.Contains("postgres"))
            return "PostgreSQL";
        if (lower.Contains("sqlclient") || lower.Contains("mssql") || lower.Contains("entityframeworkcore.sqlserver"))
            return "SQL Server";
        if (lower.Contains("sqlite"))
            return "SQLite";
        if (lower.Contains("mysql"))
            return "MySQL";
        if (lower.Contains("mongo"))
            return "MongoDB";
        if (lower.Contains("redis"))
            return "Redis";
        if (lower.Contains("clickhouse"))
            return "ClickHouse";
        return packageName;
    }

    protected virtual string MapPackageToCloudService(string packageName)
    {
        var lower = packageName.ToLowerInvariant();
        if (lower.Contains("aws") || lower.Contains("boto3"))
            return "AWS";
        if (lower.Contains("google.cloud") || lower.Contains("google-cloud") || lower.Contains("firebase"))
            return "GCP";
        if (lower.Contains("azure"))
            return "Azure";
        if (lower.Contains("stripe"))
            return "Stripe";
        return packageName;
    }
}
