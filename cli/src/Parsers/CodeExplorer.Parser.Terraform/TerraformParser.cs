using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Parser.Terraform.Hcl;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using TreeSitter;

// Auto-discovered by WorkspaceIndexer.AutoRegisterParsers via reflection.
[assembly: ParserAssembly]

namespace CodeExplorer.Parser.Terraform;

/// <summary>
/// Parser for Terraform / OpenTofu / Terragrunt (HCL). Pure C# (UsesTreeSitter => false): it lexes
/// and parses HCL in ParseAsync and builds graph nodes directly, rather than using a tree-sitter
/// grammar/visitor.
/// </summary>
public class TerraformParser : IProjectParser, IFileParser
{
    // The parser instance is reused across all files (parsed in parallel) and, in a long-running
    // host, across multiple workspaces and re-scans. Cache state is therefore scoped by workspace
    // id and bucketed by the file's directory, so one workspace never reads another's symbols and
    // an enricher reads only the bucket for its project directory. Each level is thread-safe.
    //   workspaceId -> (directory -> (relativePath -> per-file payload))
    private readonly ConcurrentDictionary<string,
        ConcurrentDictionary<string, ConcurrentDictionary<string, FileParseResult>>> _byWorkspace = new();

    // Per-file declarations + harvested references carried from ParseAsync (Layer 3) to the
    // Layer 4 enricher, since cross-file reference resolution cannot happen per file.
    internal sealed record FileParseResult(
        IReadOnlyList<TerraformSymbol> Symbols,
        IReadOnlyList<HclReference> References,
        IReadOnlyList<TerraformConfigure> Configures);

    private static string DirOf(string relativePath) =>
        (Path.GetDirectoryName(relativePath) ?? string.Empty).Replace('\\', '/').Trim('/');

    private ConcurrentDictionary<string, ConcurrentDictionary<string, FileParseResult>> WorkspaceBucket(string workspaceId) =>
        _byWorkspace.GetOrAdd(workspaceId, _ => new());

    // Returns the parsed files in one directory of one workspace (the enricher's project bucket).
    // Empty when nothing in that directory was parsed.
    internal IReadOnlyDictionary<string, FileParseResult> FilesInDirectory(string workspaceId, string directory)
    {
        var dir = directory.Replace('\\', '/').Trim('/');
        if (_byWorkspace.TryGetValue(workspaceId, out var ws) && ws.TryGetValue(dir, out var files))
        {
            return files;
        }
        return new Dictionary<string, FileParseResult>();
    }

    // Drops all cached parse state for a workspace so a re-scan in a long-running host starts clean.
    internal void ClearWorkspace(string workspaceId) => _byWorkspace.TryRemove(workspaceId, out _);

    public string LanguageName => "hcl";
    public string ProjectType => "terraform";

    public IReadOnlyList<ISemanticExtension> SemanticExtensions => [];

    // HCL is parsed in ParseAsync, never via a tree-sitter visitor.
    public BaseParserVisitor CreateVisitor(
        Node rootNode,
        List<ISemanticExtension> activeExtensions,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        SemanticExtensionRegistry extensionRegistry)
        => throw new NotSupportedException("Terraform parser uses pure C# HCL analysis (no TreeSitter visitor).");

    public bool UsesTreeSitter => false;

    // Provider cache, local state, and serverless build dirs are never descended into.
    // Directory exclusion is owned by core WorkspaceFileFilter; nothing reads this member.
    public IReadOnlyCollection<string> ExcludedFolders => [];

    public bool CanParse(string fileExtension) =>
        fileExtension.Equals(".tf", StringComparison.OrdinalIgnoreCase)
        || fileExtension.Equals(".tofu", StringComparison.OrdinalIgnoreCase)
        || fileExtension.Equals(".tfvars", StringComparison.OrdinalIgnoreCase)
        || fileExtension.Equals(".hcl", StringComparison.OrdinalIgnoreCase);

    // `.terraform.lock.hcl` is generated, not Terraform source; rejected by name. The `.hcl`
    // extension is otherwise owned for Terragrunt (terragrunt.hcl and included *.hcl fragments).
    public bool CanParseFile(string filePath) =>
        CanParse(Path.GetExtension(filePath)) && !IsLockFile(filePath);

    private static bool IsLockFile(string filePath) =>
        Path.GetFileName(filePath).Equals(".terraform.lock.hcl", StringComparison.OrdinalIgnoreCase);

    // A directory is a Terraform/Terragrunt project if it has *.tf/*.tofu or a terragrunt.hcl.
    public bool IsProjectDirectory(string directoryPath, string[] filesInDirectory)
    {
        foreach (var file in filesInDirectory)
        {
            var name = Path.GetFileName(file).ToLowerInvariant();
            if (name.EndsWith(".tf") || name.EndsWith(".tofu") || name == "terragrunt.hcl")
                return true;
        }
        return false;
    }

    public bool IsConfigurationFile(string fileName) =>
        fileName.EndsWith(".tfvars", StringComparison.OrdinalIgnoreCase);

    // Classifies an IaC directory as a library (so it is not treated as a microservice), with the
    // infrastructure intent carried in project_kind and the is_terragrunt/is_terraform_module flags.
    public Dictionary<string, string> ExtractManifestProperties(string directoryPath, string[] filesInDirectory)
    {
        var props = new Dictionary<string, string>
        {
            ["project_kind"] = "infrastructure",
            ["manifest_type"] = "library"
        };

        bool Has(string fileName) =>
            filesInDirectory.Any(f => Path.GetFileName(f).Equals(fileName, StringComparison.OrdinalIgnoreCase));

        if (Has("terragrunt.hcl"))
        {
            props["is_terragrunt"] = "true";
        }
        else if (Has("variables.tf") && Has("outputs.tf"))
        {
            props["is_terraform_module"] = "true";
        }

        return props;
    }

    // Surfaces local module sources and Terragrunt dependency/include targets as LocalProjectPaths
    // (which Layer2ProjectParser turns into project-level DEPENDS_ON); registry/git module sources
    // become external packages. Uses the HCL parser so commented-out blocks are ignored.
    public async Task<ProjectDependencyInfo> ParseDependenciesAsync(string projectDirectory)
    {
        var localProjectPaths = new List<string>();
        var externalPackages = new List<ProducedPackageInfo>();

        string[] files;
        try
        {
            files = Directory.GetFiles(projectDirectory, "*.tf", SearchOption.TopDirectoryOnly)
                .Concat(Directory.GetFiles(projectDirectory, "*.tofu", SearchOption.TopDirectoryOnly))
                .Concat(Directory.GetFiles(projectDirectory, "*.hcl", SearchOption.TopDirectoryOnly))
                .Where(f => !IsLockFile(f))
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new ProjectDependencyInfo(localProjectPaths, externalPackages);
        }

        foreach (var file in files)
        {
            string content;
            try { content = await File.ReadAllTextAsync(file); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }

            var tokens = new HclLexer(content).Tokenize();

            HclFile ast;
            try { ast = new HclParser(tokens).ParseFile(); }
            catch (Exception) { continue; }

            // read_terragrunt_config("…") loads another HCL config at parse time → a dependency on
            // that file's directory. Scanned over the lexed tokens (comments already stripped) so a
            // commented-out call does not produce a phantom edge.
            foreach (var argExpr in ExtractReadTerragruntConfigArgs(tokens))
            {
                AddLocalPath(ResolveTerragruntPath(argExpr, projectDirectory), localProjectPaths);
            }

            foreach (var block in ast.Blocks)
            {
                switch (block.Type)
                {
                    case "module":
                        AddModuleSource(AttrLiteral(block, "source"), projectDirectory, localProjectPaths, externalPackages);
                        break;
                    case "dependency":
                        AddLocalPath(ResolveTerragruntPath(AttrLiteral(block, "config_path"), projectDirectory), localProjectPaths);
                        break;
                    case "dependencies":
                        // Plural `dependencies { paths = ["../a", "../b"] }` — each list entry is a
                        // unit directory this unit depends on.
                        foreach (var p in AttrTupleItems(block, "paths"))
                        {
                            AddLocalPath(ResolveTerragruntPath(p, projectDirectory), localProjectPaths);
                        }
                        break;
                    case "include":
                        AddLocalPath(ResolveTerragruntPath(AttrLiteral(block, "path"), projectDirectory), localProjectPaths);
                        break;
                    case "terraform":
                        // Terragrunt `terraform { source = … }` points at a local module, often via
                        // a path function / interpolation (e.g. "${get_terragrunt_dir()}/../mod").
                        AddModuleSource(AttrLiteral(block, "source"), projectDirectory, localProjectPaths, externalPackages);
                        break;
                }
            }
        }

        return new ProjectDependencyInfo(
            localProjectPaths.Distinct().ToList(),
            externalPackages);
    }

    // A module/terraform source is local when it resolves to a filesystem path — either a plain
    // relative/absolute path or a Terragrunt path expression (`${get_terragrunt_dir()}/…`). When it
    // does not resolve (registry or git source like "terraform-aws-modules/vpc/aws" or "git::…"),
    // it is an external package.
    private static void AddModuleSource(string? source, string projectDirectory, List<string> localProjectPaths, List<ProducedPackageInfo> externalPackages)
    {
        if (string.IsNullOrEmpty(source)) return;

        var resolved = ResolveTerragruntPath(source, projectDirectory);
        if (!string.IsNullOrEmpty(resolved))
        {
            localProjectPaths.Add(NormalizeToProjectDir(resolved)!);
        }
        else
        {
            externalPackages.Add(new ProducedPackageInfo(source, "unknown", "terraform"));
        }
    }

    private static void AddLocalPath(string? path, List<string> localProjectPaths)
    {
        var normalized = NormalizeToProjectDir(path);
        if (!string.IsNullOrEmpty(normalized)) localProjectPaths.Add(normalized!);
    }

    // Projects are keyed by directory, but a resolved Terragrunt path can point at a file (e.g.
    // "…/_service_modules/x.hcl" or a config file). Map such a path to its containing directory so
    // Layer 2 matches a real project node instead of a dangling file-named id.
    private static string? NormalizeToProjectDir(string? path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        var trimmed = path.TrimEnd('/');
        var ext = Path.GetExtension(trimmed);
        if (ext.Equals(".hcl", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".tf", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".tofu", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".tfvars", StringComparison.OrdinalIgnoreCase))
        {
            var dir = Path.GetDirectoryName(trimmed);
            return string.IsNullOrEmpty(dir) ? trimmed : dir!.Replace('\\', '/');
        }
        return path;
    }

    /// <summary>
    /// Resolves a Terragrunt unit's <c>terraform { source }</c> to the wrapped module's directory
    /// relative to the workspace, for the enricher's <c>inputs → CONFIGURES</c> resolution. Returns
    /// null when the source is empty, a remote/registry/git source, or resolves outside the
    /// workspace — in those cases the target module's variables are not in the graph.
    /// </summary>
    internal static string? ResolveModuleSourceDir(string? rawSource, string unitRelativeDir, string absoluteWorkspacePath)
    {
        if (string.IsNullOrWhiteSpace(rawSource)) return null;

        var unitAbs = Path.GetFullPath(Path.Combine(absoluteWorkspacePath, unitRelativeDir));
        var resolved = ResolveTerragruntPath(rawSource, unitAbs);
        if (string.IsNullOrEmpty(resolved)) return null;

        var absTarget = Path.IsPathRooted(resolved)
            ? resolved
            : Path.GetFullPath(Path.Combine(unitAbs, resolved));

        var rel = Path.GetRelativePath(absoluteWorkspacePath, absTarget).Replace('\\', '/').Trim('/');
        // Outside the workspace (e.g. git/registry source resolving nowhere local).
        if (rel.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(rel)) return null;
        return rel;
    }

    private static string? AttrLiteral(HclBlock block, string attrName)
    {
        foreach (var a in block.Attributes)
        {
            if (string.Equals(a.Name, attrName, StringComparison.Ordinal)) return a.Value.Literal;
        }
        return null;
    }

    // Returns the top-level string elements of a list-valued attribute (e.g. the entries of
    // `paths = ["../a", "../b"]`), captured structurally by the parser.
    private static IReadOnlyList<string> AttrTupleItems(HclBlock block, string attrName)
    {
        foreach (var a in block.Attributes)
        {
            if (string.Equals(a.Name, attrName, StringComparison.Ordinal))
            {
                return a.Value.TupleItems ?? [];
            }
        }
        return [];
    }

    // Scans lexed tokens for read_terragrunt_config(<arg>) calls and returns each argument as a
    // reconstructed expression string (e.g. find_in_parent_folders("common_vars.hcl")), suitable
    // for ResolveTerragruntPath. String tokens are re-quoted since the lexer strips their quotes.
    private static IEnumerable<string> ExtractReadTerragruntConfigArgs(IReadOnlyList<HclToken> tokens)
    {
        for (var i = 0; i < tokens.Count - 1; i++)
        {
            if (tokens[i].Kind != HclTokenKind.Identifier ||
                !string.Equals(tokens[i].Text, "read_terragrunt_config", StringComparison.Ordinal) ||
                tokens[i + 1].Kind != HclTokenKind.LParen)
            {
                continue;
            }

            // Reconstruct the argument text up to the matching close paren.
            var sb = new System.Text.StringBuilder();
            var depth = 0;
            var j = i + 1;
            for (; j < tokens.Count; j++)
            {
                var t = tokens[j];
                if (t.Kind == HclTokenKind.LParen) { depth++; if (depth == 1) continue; }
                else if (t.Kind == HclTokenKind.RParen) { depth--; if (depth == 0) break; }

                sb.Append(t.Kind == HclTokenKind.String ? $"\"{t.Text}\"" : t.Text);
            }

            var arg = sb.ToString().Trim();
            if (arg.Length > 0) yield return arg;
            i = j; // continue scanning after this call
        }
    }

    /// <summary>
    /// Best-effort resolution of a Terragrunt path expression to a local directory path, handling
    /// the common path functions. Returns a path (relative or absolute) suitable as a local
    /// dependency, or null if it can't be resolved to a local target.
    ///   find_in_parent_folders()            -> nearest ancestor containing a terragrunt.hcl (the root)
    ///   find_in_parent_folders("file.hcl")  -> nearest ancestor containing that file
    ///   dirname(expr)                       -> parent directory of the inner resolution
    ///   get_repo_root() / get_terragrunt_dir() -> repo root / this unit dir (best-effort)
    ///   plain "../x" / "./x"                -> relative path as-is
    /// Interpolation wrappers ("${ ... }/suffix") are unwrapped to the inner expression.
    /// </summary>
    private static string? ResolveTerragruntPath(string? expr, string projectDirectory)
    {
        if (string.IsNullOrWhiteSpace(expr)) return null;
        expr = expr.Trim().Trim('"');

        // "${ <inner> }<suffix>" → resolve <inner>, then re-append the literal suffix (e.g.
        // "${dirname(find_in_parent_folders())}/_service_modules/x.hcl").
        var interp = Regex.Match(expr, @"^\$\{\s*(?<inner>.*?)\s*\}(?<suffix>.*)$");
        if (interp.Success)
        {
            var resolvedInner = ResolveTerragruntPath(interp.Groups["inner"].Value, projectDirectory);
            if (string.IsNullOrEmpty(resolvedInner)) return null;
            var suffix = interp.Groups["suffix"].Value;
            return resolvedInner!.TrimEnd('/') + suffix;
        }

        // dirname(<inner>) → parent of inner's resolution.
        var dirn = Regex.Match(expr, @"^dirname\s*\(\s*(?<inner>.*?)\s*\)$");
        if (dirn.Success)
        {
            var inner = ResolveTerragruntPath(dirn.Groups["inner"].Value, projectDirectory);
            if (string.IsNullOrEmpty(inner)) return null;
            try { return Path.GetDirectoryName(inner!.TrimEnd('/')); }
            catch { return null; }
        }

        if (expr.StartsWith("find_in_parent_folders", StringComparison.Ordinal))
        {
            // The named file (or root terragrunt.hcl) lives in the nearest ancestor that has it.
            var arg = Regex.Match(expr, @"find_in_parent_folders\s*\(\s*""(?<f>[^""]*)""\s*\)");
            var fileName = arg.Success ? arg.Groups["f"].Value : "terragrunt.hcl";
            return FindInParentFolders(projectDirectory, fileName);
        }

        if (expr.StartsWith("get_repo_root", StringComparison.Ordinal))
        {
            return FindRepoRoot(projectDirectory);
        }

        if (expr.StartsWith("get_terragrunt_dir", StringComparison.Ordinal))
        {
            return projectDirectory;
        }

        // A plain relative/absolute path (possibly with a trailing "/suffix" after an unwrap).
        if (expr.StartsWith('.') || expr.StartsWith('/'))
        {
            return expr;
        }

        return null;
    }

    private static string? FindInParentFolders(string startDir, string fileName)
    {
        try
        {
            var dir = Directory.GetParent(Path.GetFullPath(startDir));
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, fileName)))
                    return dir.FullName.Replace('\\', '/');
                dir = dir.Parent;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return null;
    }

    private static string? FindRepoRoot(string startDir)
    {
        try
        {
            var dir = new DirectoryInfo(Path.GetFullPath(startDir));
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
                    return dir.FullName.Replace('\\', '/');
                dir = dir.Parent;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return null;
    }

    public Task<ProducedPackageInfo?> GetProducedPackageAsync(string projectDirectory)
        => Task.FromResult<ProducedPackageInfo?>(null);

    // The Layer 4 enricher resolves the references collected in ParseAsync into DEPENDS_ON edges,
    // scoped to the module directory.
    public ISyntaxEnricher GetSyntaxEnricher(SyntaxTree syntaxTree)
        => new TerraformSyntaxEnricher(this);

    // Lexes and parses the HCL file, emits TypeNode/MemberNode declarations into the FileNode with
    // workspace-scoped URN IDs, and stashes the declared symbols + harvested references for the
    // Layer 4 enricher (cross-file reference resolution cannot happen per-file).
    public async Task<SyntaxTree> ParseAsync(string filePath, string parentNodeId, string workspaceId, string absoluteWorkspacePath)
    {
        var relativePath = Path.GetRelativePath(absoluteWorkspacePath, filePath).Replace('\\', '/');
        var fileName = Path.GetFileName(filePath);
        var fileNodeId = $"{workspaceId}:{OntologyConstants.IdPrefixes.File}:{relativePath}";
        var fileNode = new FileNode(fileNodeId, fileName, relativePath, filePath);

        // A lock file may still reach here via the extension-only gate; keep its File node for
        // physical topology but do not parse its contents into declarations.
        if (IsLockFile(filePath))
        {
            return new SyntaxTree(
                filePath, relativePath, null, null, null, fileNode, this, [], [], []);
        }

        try
        {
            var content = await File.ReadAllTextAsync(filePath);
            var tokens = new HclLexer(content).Tokenize();
            var ast = new HclParser(tokens).ParseFile();

            var symbols = TerraformDeclarationMapper.Map(ast, fileNode, workspaceId, relativePath);
            var configures = TerraformDeclarationMapper.MapConfigures(ast, relativePath);

            var bucket = WorkspaceBucket(workspaceId).GetOrAdd(DirOf(relativePath), _ => new());
            bucket[relativePath] = new FileParseResult(symbols, ast.References, configures);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Unreadable file: keep the empty FileNode so physical topology stays intact.
            Console.Error.WriteLine($"[Terraform] Could not read '{relativePath}': {ex.Message}");
        }
        catch (Exception ex)
        {
            // Parse failure: keep the empty FileNode and report the error.
            Console.Error.WriteLine($"[Terraform] Failed to parse '{relativePath}': {ex}");
        }

        return new SyntaxTree(
            filePath, relativePath, null, null, null, fileNode, this, [], [], []);
    }

    // Local module sources ("./", "../", "/") are internal; registry/git sources are external.
    public ImportType ResolveImportType(string importPath, string filePath, string? absoluteWorkspacePath)
    {
        if (string.IsNullOrEmpty(importPath)) return ImportType.External;
        return importPath.StartsWith('.') || importPath.StartsWith('/')
            ? ImportType.Internal
            : ImportType.External;
    }
}
