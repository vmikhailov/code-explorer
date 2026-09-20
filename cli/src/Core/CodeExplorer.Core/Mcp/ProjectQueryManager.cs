using System.Text.Json;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Mcp.Models;
using CodeExplorer.Cypher.Compiler;
using CodeExplorer.Cypher.Parser;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeExplorer.Core.Mcp;

public class ProjectQueryManager(ILogger<ProjectQueryManager>? logger = null)
{
    private static readonly Regex SafeNameRegex = new(@"^[a-zA-Z0-9_-]+$", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ILogger _logger = (ILogger?)logger ?? NullLogger.Instance;

    public string GetQueriesDirectory(string workspacePath) =>
        Path.Combine(workspacePath, ".codeexplorer", "queries");

    public void ValidateQuerySecurity(string cypher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cypher);

        CypherSecurityValidator.ValidateReadOnly(cypher);
    }

    public void ValidateQuerySyntax(string cypher)
    {
        ValidateQuerySecurity(cypher);

        try
        {
            var ast = CypherQueryParser.Parse(cypher);
            SqliteCompiler.Compile(ast);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Cypher syntax validation failed: {ex.Message}", ex);
        }
    }

    public IReadOnlyList<ProjectQueryItem> ListQueries(string workspacePath)
    {
        var dir = GetQueriesDirectory(workspacePath);
        if (!Directory.Exists(dir))
        {
            return [];
        }

        var cypherFiles = Directory.GetFiles(dir, "*.cypher");
        var items = new List<ProjectQueryItem>(cypherFiles.Length);

        foreach (var cypherPath in cypherFiles)
        {
            var name = Path.GetFileNameWithoutExtension(cypherPath);
            var metadataPath = Path.Combine(dir, $"{name}.json");
            var cypherText = File.ReadAllText(cypherPath);

            ProjectQueryMetadata? metadata = null;
            if (File.Exists(metadataPath))
            {
                try
                {
                    var json = File.ReadAllText(metadataPath);
                    metadata = JsonSerializer.Deserialize<ProjectQueryMetadata>(json);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse metadata for project query '{Name}' at {Path}", name, metadataPath);
                }
            }

            metadata ??= new ProjectQueryMetadata
            {
                Name = name,
                Description = $"Custom project query: {name}",
                Parameters = [],
                Tags = []
            };

            items.Add(new ProjectQueryItem(
                Name: name,
                Description: metadata.Description,
                Parameters: metadata.Parameters,
                Returns: metadata.Returns,
                Tags: metadata.Tags,
                Cypher: cypherText,
                CypherPath: cypherPath,
                MetadataPath: metadataPath
            ));
        }

        return items;
    }

    public ProjectQueryItem? GetQuery(string workspacePath, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        var dir = GetQueriesDirectory(workspacePath);
        var cypherPath = Path.Combine(dir, $"{name}.cypher");
        if (!File.Exists(cypherPath)) return null;

        var metadataPath = Path.Combine(dir, $"{name}.json");
        var cypherText = File.ReadAllText(cypherPath);

        ProjectQueryMetadata? metadata = null;
        if (File.Exists(metadataPath))
        {
            try
            {
                var json = File.ReadAllText(metadataPath);
                metadata = JsonSerializer.Deserialize<ProjectQueryMetadata>(json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse metadata for project query '{Name}' at {Path}", name, metadataPath);
            }
        }

        metadata ??= new ProjectQueryMetadata
        {
            Name = name,
            Description = $"Custom project query: {name}",
            Parameters = [],
            Tags = []
        };

        return new ProjectQueryItem(
            Name: name,
            Description: metadata.Description,
            Parameters: metadata.Parameters,
            Returns: metadata.Returns,
            Tags: metadata.Tags,
            Cypher: cypherText,
            CypherPath: cypherPath,
            MetadataPath: metadataPath
        );
    }

    public ProjectQueryItem SaveQuery(
        string workspacePath,
        string name,
        string cypher,
        ProjectQueryMetadata metadata)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        name = name.Trim();
        if (!SafeNameRegex.IsMatch(name))
        {
            throw new ArgumentException(
                $"Invalid query name '{name}'. Names must contain only alphanumeric characters, underscores, and hyphens.",
                nameof(name));
        }

        // Validate syntax and read-only safety
        ValidateQuerySyntax(cypher);

        var dir = GetQueriesDirectory(workspacePath);
        Directory.CreateDirectory(dir);

        var cypherPath = Path.Combine(dir, $"{name}.cypher");
        var metadataPath = Path.Combine(dir, $"{name}.json");

        var normalizedMetadata = metadata with
        {
            Name = name,
            Description = string.IsNullOrWhiteSpace(metadata.Description) ? $"Custom project query: {name}" : metadata.Description
        };

        File.WriteAllText(cypherPath, cypher.Trim());
        var metaJson = JsonSerializer.Serialize(normalizedMetadata, JsonOptions);
        File.WriteAllText(metadataPath, metaJson);

        _logger.LogInformation("Saved project query '{Name}' at {Path}", name, cypherPath);

        return new ProjectQueryItem(
            Name: name,
            Description: normalizedMetadata.Description,
            Parameters: normalizedMetadata.Parameters,
            Returns: normalizedMetadata.Returns,
            Tags: normalizedMetadata.Tags,
            Cypher: cypher.Trim(),
            CypherPath: cypherPath,
            MetadataPath: metadataPath
        );
    }

    public bool DeleteQuery(string workspacePath, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;

        var dir = GetQueriesDirectory(workspacePath);
        var cypherPath = Path.Combine(dir, $"{name}.cypher");
        var metadataPath = Path.Combine(dir, $"{name}.json");

        var deleted = false;
        if (File.Exists(cypherPath))
        {
            File.Delete(cypherPath);
            deleted = true;
        }

        if (File.Exists(metadataPath))
        {
            File.Delete(metadataPath);
            deleted = true;
        }

        return deleted;
    }
}
