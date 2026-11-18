using CodeExplorer.Parsers.Terraform.Hcl;

namespace CodeExplorer.Parsers.Terraform;

/// <summary>
/// Resolves Terraform module sources and Terragrunt path expressions (<c>find_in_parent_folders</c>,
/// <c>dirname</c>, <c>get_repo_root</c>, <c>get_terragrunt_dir</c>, and double-slash <c>//</c> subpaths)
/// directly from <see cref="HclExpr"/> AST nodes without regex string reconstruction.
/// </summary>
public static class TerraformPathResolver
{
    private const string FnFindInParentFolders = "find_in_parent_folders";
    private const string FnDirname = "dirname";
    private const string FnGetRepoRoot = "get_repo_root";
    private const string FnGetTerragruntDir = "get_terragrunt_dir";
    private const string FnGetParentTerragruntDir = "get_parent_terragrunt_dir";
    private const string FnPathRelativeToInclude = "path_relative_to_include";
    private const string DefaultTerragruntFile = "terragrunt.hcl";
    private const string GitMarker = ".git";
    private const string DoubleSlash = "//";
    private const string InterpPrefixRepoRoot = "${get_repo_root()}";
    private const string InterpPrefixTerragruntDir = "${get_terragrunt_dir()}";
    private const string InterpPrefixParentTerragruntDir = "${get_parent_terragrunt_dir()}";

    private static readonly string[] RemotePrefixes =
    [
        "git::", "hg::", "s3::", "gcs::",
        "http://", "https://", "ssh://",
        "github.com/", "bitbucket.org/", "gitlab.com/",
        "registry.terraform.io/", "registry.opentofu.org/"
    ];

    public static bool IsLocalModuleSource(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return false;
        return source.StartsWith("./", StringComparison.Ordinal) ||
               source.StartsWith("../", StringComparison.Ordinal) ||
               source.StartsWith(".\\", StringComparison.Ordinal) ||
               source.StartsWith("..\\", StringComparison.Ordinal);
    }

    public static bool IsRemoteSource(string source)
    {
        foreach (var prefix in RemotePrefixes)
        {
            if (source.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    public static string StripModuleSubpath(string source)
    {
        var idx = source.IndexOf(DoubleSlash, StringComparison.Ordinal);
        if (idx < 0) return source;
        if (idx > 0 && source[idx - 1] == ':')
        {
            var secondIdx = source.IndexOf(DoubleSlash, idx + 2, StringComparison.Ordinal);
            return secondIdx < 0 ? source : $"{source[..secondIdx]}/{source[(secondIdx + 2)..]}";
        }
        return $"{source[..idx]}/{source[(idx + 2)..]}";
    }

    /// <summary>
    /// Evaluates an <see cref="HclExpr"/> representing a Terragrunt path or module source
    /// using the provided file/directory existence predicates.
    /// </summary>
    public static string? EvaluatePathExpr(
        HclExpr? expr,
        string baseDir,
        string workspaceRootDir,
        Func<string, bool> fileExists,
        Func<string, bool> directoryExists,
        IReadOnlyDictionary<string, string>? localStrings = null)
    {
        if (expr == null) return null;

        switch (expr)
        {
            case HclExpr.Literal lit:
                return ResolveStringPath(lit.Value, baseDir, workspaceRootDir, fileExists, directoryExists, localStrings);

            case HclExpr.Template tpl:
                return EvaluateTemplatePath(tpl, baseDir, workspaceRootDir, fileExists, directoryExists, localStrings);

            case HclExpr.Call call:
                return EvaluateCallPath(call, baseDir, workspaceRootDir, fileExists, directoryExists, localStrings);

            case HclExpr.Traversal tr when localStrings != null && localStrings.TryGetValue(tr.Joined, out var resolvedLocal):
                return ResolveStringPath(resolvedLocal, baseDir, workspaceRootDir, fileExists, directoryExists, localStrings);

            default:
                return null;
        }
    }

    private static string? EvaluateCallPath(
        HclExpr.Call call,
        string baseDir,
        string workspaceRootDir,
        Func<string, bool> fileExists,
        Func<string, bool> directoryExists,
        IReadOnlyDictionary<string, string>? localStrings)
    {
        if (string.Equals(call.FunctionName, FnFindInParentFolders, StringComparison.Ordinal))
        {
            var fileName = call.Arguments.Count > 0
                ? call.Arguments[0].TryGetStringLiteral() ?? DefaultTerragruntFile
                : DefaultTerragruntFile;
            return FindInParentFolders(baseDir, workspaceRootDir, fileName, fileExists);
        }

        if (string.Equals(call.FunctionName, FnDirname, StringComparison.Ordinal) && call.Arguments.Count > 0)
        {
            var inner = EvaluatePathExpr(call.Arguments[0], baseDir, workspaceRootDir, fileExists, directoryExists, localStrings);
            return string.IsNullOrEmpty(inner) ? null : NormalizeDirPath(Path.GetDirectoryName(inner));
        }

        if (string.Equals(call.FunctionName, FnGetRepoRoot, StringComparison.Ordinal))
        {
            return NormalizeDirPath(workspaceRootDir);
        }

        if (string.Equals(call.FunctionName, FnGetTerragruntDir, StringComparison.Ordinal) ||
            string.Equals(call.FunctionName, FnGetParentTerragruntDir, StringComparison.Ordinal))
        {
            return NormalizeDirPath(baseDir);
        }

        if (string.Equals(call.FunctionName, FnPathRelativeToInclude, StringComparison.Ordinal))
        {
            return ".";
        }

        return null;
    }

    private static string? EvaluateTemplatePath(
        HclExpr.Template tpl,
        string baseDir,
        string workspaceRootDir,
        Func<string, bool> fileExists,
        Func<string, bool> directoryExists,
        IReadOnlyDictionary<string, string>? localStrings)
    {
        if (tpl.Interpolations.Count == 1 &&
            tpl.RawValue.StartsWith("${", StringComparison.Ordinal) &&
            tpl.RawValue.EndsWith('}') &&
            tpl.RawValue.IndexOf('}', 2) == tpl.RawValue.Length - 1)
        {
            return EvaluatePathExpr(tpl.Interpolations[0], baseDir, workspaceRootDir, fileExists, directoryExists, localStrings);
        }

        return ResolveStringPath(tpl.RawValue, baseDir, workspaceRootDir, fileExists, directoryExists, localStrings);
    }

    public static string? ResolveStringPath(
        string? raw,
        string baseDir,
        string workspaceRootDir,
        Func<string, bool> fileExists,
        Func<string, bool> directoryExists,
        IReadOnlyDictionary<string, string>? localStrings = null)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var s = raw.Trim();
        if (localStrings is { Count: > 0 } && s.Contains("${local.", StringComparison.Ordinal))
        {
            foreach (var (k, v) in localStrings)
            {
                var token = $"${{{k}}}";
                if (s.Contains(token, StringComparison.Ordinal))
                {
                    s = s.Replace(token, v, StringComparison.Ordinal);
                }
            }
        }

        if (IsRemoteSource(s))
        {
            return TryResolveWorkspaceSiblingRepo(s, baseDir, workspaceRootDir, directoryExists);
        }

        var hadRepoRootPrefix = s.Contains(InterpPrefixRepoRoot, StringComparison.Ordinal);
        var rawBeforeRepoRoot = s;
        if (hadRepoRootPrefix)
        {
            s = s.Replace(InterpPrefixRepoRoot, NormalizeDirPath(workspaceRootDir), StringComparison.Ordinal);
        }
        if (s.Contains(InterpPrefixTerragruntDir, StringComparison.Ordinal))
        {
            s = s.Replace(InterpPrefixTerragruntDir, NormalizeDirPath(baseDir), StringComparison.Ordinal);
        }
        if (s.Contains(InterpPrefixParentTerragruntDir, StringComparison.Ordinal))
        {
            var parent = FindInParentFolders(baseDir, workspaceRootDir, DefaultTerragruntFile, fileExists);
            var parentDir = parent != null ? NormalizeDirPath(Path.GetDirectoryName(parent)) : NormalizeDirPath(baseDir);
            s = s.Replace(InterpPrefixParentTerragruntDir, parentDir, StringComparison.Ordinal);
        }

        if (s.Contains("${", StringComparison.Ordinal))
        {
            return TryResolveWorkspaceSiblingRepo(s, baseDir, workspaceRootDir, directoryExists);
        }

        var stripped = StripModuleSubpath(s);
        var candidate = Path.IsPathRooted(stripped)
            ? NormalizeDirPath(Path.GetFullPath(stripped))
            : NormalizeDirPath(Path.GetFullPath(Path.Combine(baseDir, stripped)));

        if (directoryExists(candidate))
        {
            return candidate;
        }
        if (fileExists(candidate))
        {
            return NormalizeDirPath(Path.GetDirectoryName(candidate));
        }

        if (hadRepoRootPrefix)
        {
            var subrepoRoot = GetEnclosingSubrepoRoot(baseDir, workspaceRootDir);
            if (!string.IsNullOrEmpty(subrepoRoot))
            {
                var subCandidate = StripModuleSubpath(
                    rawBeforeRepoRoot.Replace(InterpPrefixRepoRoot, subrepoRoot, StringComparison.Ordinal));
                var normSub = NormalizeDirPath(Path.GetFullPath(subCandidate));
                if (directoryExists(normSub))
                {
                    return normSub;
                }
                if (fileExists(normSub))
                {
                    return NormalizeDirPath(Path.GetDirectoryName(normSub));
                }
            }
        }

        return TryResolveWorkspaceSiblingRepo(s, baseDir, workspaceRootDir, directoryExists);
    }

    public static string? TryResolveWorkspaceSiblingRepo(
        string? raw,
        string baseDir,
        string workspaceRootDir,
        Func<string, bool> directoryExists)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var s = raw.Trim();
        var qIdx = s.IndexOf('?');
        if (qIdx >= 0)
        {
            s = s[..qIdx];
        }

        var schemeIdx = s.IndexOf("://", StringComparison.Ordinal);
        var searchStart = schemeIdx >= 0 ? schemeIdx + 3 : 0;
        var doubleSlashIdx = s.IndexOf(DoubleSlash, searchStart, StringComparison.Ordinal);

        string repoPart;
        string subpathPart;
        if (doubleSlashIdx >= 0)
        {
            repoPart = s[..doubleSlashIdx].TrimEnd('/');
            subpathPart = s[(doubleSlashIdx + 2)..].Trim('/');
        }
        else
        {
            repoPart = s.TrimEnd('/');
            subpathPart = string.Empty;
        }

        var lastSep = repoPart.LastIndexOfAny(['/', ':', '}']);
        var repoName = lastSep >= 0 ? repoPart[(lastSep + 1)..] : repoPart;
        if (repoName.EndsWith(GitMarker, StringComparison.OrdinalIgnoreCase))
        {
            repoName = repoName[..^GitMarker.Length];
        }

        var normWsRoot = NormalizeDirPath(workspaceRootDir);
        var parentWsRoot = NormalizeDirPath(Path.GetDirectoryName(normWsRoot));
        var subrepoRoot = GetEnclosingSubrepoRoot(baseDir, normWsRoot);

        if (!string.IsNullOrEmpty(repoName) && !repoName.Contains('$'))
        {
            var relFromRepo = string.IsNullOrEmpty(subpathPart) ? repoName : $"{repoName}/{subpathPart}";
            if (!string.IsNullOrEmpty(normWsRoot))
            {
                var wsCandidate = $"{normWsRoot}/{relFromRepo}";
                if (directoryExists(wsCandidate)) return wsCandidate;
            }
            if (!string.IsNullOrEmpty(parentWsRoot))
            {
                var parentCandidate = $"{parentWsRoot}/{relFromRepo}";
                if (directoryExists(parentCandidate)) return parentCandidate;
            }
        }

        if (!string.IsNullOrEmpty(subpathPart) && !subpathPart.Contains('$'))
        {
            if (!string.IsNullOrEmpty(subrepoRoot))
            {
                var subCandidate = $"{subrepoRoot}/{subpathPart}";
                if (directoryExists(subCandidate)) return subCandidate;
            }
            if (!string.IsNullOrEmpty(normWsRoot))
            {
                var wsSubCandidate = $"{normWsRoot}/{subpathPart}";
                if (directoryExists(wsSubCandidate)) return wsSubCandidate;
            }
        }

        return null;
    }

    private static string? GetEnclosingSubrepoRoot(string baseDir, string workspaceRootDir)
    {
        var normBase = NormalizeDirPath(baseDir);
        var normRoot = NormalizeDirPath(workspaceRootDir);
        if (string.IsNullOrEmpty(normBase) || string.IsNullOrEmpty(normRoot)) return null;

        var prefix = normRoot + "/";
        if (!normBase.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;

        var remainder = normBase[prefix.Length..];
        var slashIdx = remainder.IndexOf('/');
        var firstSegment = slashIdx >= 0 ? remainder[..slashIdx] : remainder;
        return string.IsNullOrEmpty(firstSegment) ? null : $"{normRoot}/{firstSegment}";
    }

    public static string? FindInParentFolders(
        string startDir,
        string workspaceRootDir,
        string fileName,
        Func<string, bool> fileExists)
    {
        var normRoot = NormalizeDirPath(workspaceRootDir);
        var current = Path.GetDirectoryName(NormalizeDirPath(startDir));

        while (!string.IsNullOrEmpty(current))
        {
            var normCur = NormalizeDirPath(current);
            var candidate = $"{normCur}/{fileName}";
            if (fileExists(candidate))
            {
                return candidate;
            }

            if (string.Equals(normCur, normRoot, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            var parent = Path.GetDirectoryName(normCur);
            if (string.IsNullOrEmpty(parent) || string.Equals(parent, normCur, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
            current = parent;
        }

        return null;
    }

    public static string NormalizeDirPath(string? path) =>
        string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/').TrimEnd('/');
}
