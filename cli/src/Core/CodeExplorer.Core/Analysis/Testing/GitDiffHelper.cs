using System.Diagnostics;
using System.Text.RegularExpressions;

namespace CodeExplorer.Core.Analysis.Testing;

public static class GitDiffHelper
{
    private static readonly Regex FileHeaderRegex = new(@"^\+\+\+ b/(.+)$", RegexOptions.Compiled);
    private static readonly Regex HunkHeaderRegex = new(@"^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@", RegexOptions.Compiled);

    /// <summary>
    /// Parses unified diff output (e.g. git diff -U0) into file hunk ranges.
    /// </summary>
    public static List<ChangedHunk> ParseDiffHunks(string? diffContent)
    {
        var result = new List<ChangedHunk>();
        if (string.IsNullOrWhiteSpace(diffContent)) return result;

        string? currentFile = null;
        using var reader = new StringReader(diffContent);
        string? line;

        while ((line = reader.ReadLine()) != null)
        {
            var matchFile = FileHeaderRegex.Match(line);
            if (matchFile.Success)
            {
                currentFile = matchFile.Groups[1].Value.Trim().Replace('\\', '/');
                continue;
            }

            var matchHunk = HunkHeaderRegex.Match(line);
            if (matchHunk.Success && currentFile != null)
            {
                int start = int.Parse(matchHunk.Groups[3].Value);
                int count = matchHunk.Groups[4].Success ? int.Parse(matchHunk.Groups[4].Value) : 1;
                int end = count > 0 ? start + count - 1 : start;
                result.Add(new ChangedHunk(currentFile, start, end));
            }
        }

        return result;
    }

    /// <summary>
    /// Executes git command to fetch diff output. Returns null if git fails or folder is not a repository.
    /// </summary>
    public static async Task<string?> GetGitDiffAsync(
        string? workspaceRoot,
        string? baseBranch = null,
        CancellationToken cancellationToken = default)
    {
        var root = string.IsNullOrWhiteSpace(workspaceRoot) ? Directory.GetCurrentDirectory() : workspaceRoot;

        var safeBase = !string.IsNullOrWhiteSpace(baseBranch) && !baseBranch.StartsWith("-")
            ? baseBranch
            : null;

        var args = !string.IsNullOrWhiteSpace(safeBase)
            ? $"diff -U0 {safeBase}"
            : "diff -U0 HEAD";

        return await RunGitCommandAsync(root, args, cancellationToken);
    }

    /// <summary>
    /// Fetches list of changed file names from git.
    /// </summary>
    public static async Task<List<string>> GetGitChangedFilesAsync(
        string? workspaceRoot,
        string? baseBranch = null,
        CancellationToken cancellationToken = default)
    {
        var root = string.IsNullOrWhiteSpace(workspaceRoot) ? Directory.GetCurrentDirectory() : workspaceRoot;
        var args = !string.IsNullOrWhiteSpace(baseBranch)
            ? $"diff --name-only {baseBranch}"
            : "diff --name-only HEAD";

        var output = await RunGitCommandAsync(root, args, cancellationToken);
        if (string.IsNullOrWhiteSpace(output)) return new List<string>();

        return output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(f => f.Trim().Replace('\\', '/'))
            .Where(f => !string.IsNullOrEmpty(f))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task<string?> RunGitCommandAsync(string workingDirectory, string arguments, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return null;

            var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = proc.StandardError.ReadToEndAsync(ct);

            await proc.WaitForExitAsync(ct);
            if (proc.ExitCode != 0) return null;

            return await stdoutTask;
        }
        catch
        {
            return null;
        }
    }
}
