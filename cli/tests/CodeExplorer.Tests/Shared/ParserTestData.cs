using NUnit.Framework;

namespace CodeExplorer.Tests.Shared;

public static class ParserTestData
{
    public static string LocateTestData(string relativePath)
    {
        if (Path.IsPathRooted(relativePath) && (File.Exists(relativePath) || Directory.Exists(relativePath)))
        {
            return relativePath;
        }

        var baseDir = TestContext.CurrentContext.TestDirectory;
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var direct = Path.Combine(baseDir, "TestData", normalized);
        if (File.Exists(direct) || Directory.Exists(direct))
        {
            return direct;
        }

        // Direct check in baseDir
        var directUnderBase = Path.Combine(baseDir, normalized);
        if (File.Exists(directUnderBase) || Directory.Exists(directUnderBase))
        {
            return directUnderBase;
        }

        // Search upwards for solution root
        var curr = new DirectoryInfo(baseDir);
        while (curr != null && !File.Exists(Path.Combine(curr.FullName, "CodeExplorer.slnx")))
        {
            curr = curr.Parent;
        }

        if (curr != null)
        {
            var inSourceWithTestData = Path.Combine(curr.FullName, "tests", "CodeExplorer.Tests", "TestData", normalized);
            if (File.Exists(inSourceWithTestData) || Directory.Exists(inSourceWithTestData))
            {
                return inSourceWithTestData;
            }

            var inSource = Path.Combine(curr.FullName, "tests", "CodeExplorer.Tests", normalized);
            if (File.Exists(inSource) || Directory.Exists(inSource))
            {
                return inSource;
            }
        }

        throw new FileNotFoundException($"Could not locate test data: '{relativePath}' (checked baseDir='{baseDir}')");
    }

    public static TempFile GetPreparedFile(string relativePath)
    {
        var sourceFile = LocateTestData(relativePath);
        if (!File.Exists(sourceFile))
        {
            throw new FileNotFoundException($"Test fixture file not found: '{sourceFile}'");
        }

        var fileName = Path.GetFileName(sourceFile);
        if (fileName.EndsWith(".test", StringComparison.OrdinalIgnoreCase))
        {
            fileName = fileName[..^5];
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "ce_file_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var targetFile = Path.Combine(tempDir, fileName);
        File.Copy(sourceFile, targetFile, true);

        return new TempFile(tempDir, targetFile);
    }

    public static TempWorkspace PrepareTempWorkspace(string workspaceRelativePath)
    {
        var sourceDir = LocateTestData(workspaceRelativePath);
        if (!Directory.Exists(sourceDir))
        {
            throw new DirectoryNotFoundException($"Test workspace directory not found: '{sourceDir}'");
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "ce_ws_" + Guid.NewGuid().ToString("N")).Replace('\\', '/');
        Directory.CreateDirectory(tempDir);

        CopyDirectoryWithSuffixStrip(sourceDir, tempDir);

        return new TempWorkspace(tempDir);
    }

    private static void CopyDirectoryWithSuffixStrip(string sourceDir, string targetDir)
    {
        foreach (var dir in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relativeDir = Path.GetRelativePath(sourceDir, dir);
            Directory.CreateDirectory(Path.Combine(targetDir, relativeDir));
        }

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, file);
            if (relative.EndsWith(".test", StringComparison.OrdinalIgnoreCase))
            {
                relative = relative[..^5];
            }

            var dest = Path.Combine(targetDir, relative);
            var parent = Path.GetDirectoryName(dest);
            if (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent))
            {
                Directory.CreateDirectory(parent);
            }

            File.Copy(file, dest, true);
        }
    }

    public static IEnumerable<TestCaseData> GetFiles(string directory, string searchPattern = "*.test")
    {
        var targetDir = LocateTestData(directory);
        var files = Directory.EnumerateFiles(targetDir, searchPattern, SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var relativeName = Path.GetRelativePath(targetDir, file).Replace('\\', '/');
            var testName = Path.GetFileName(file);
            if (testName.EndsWith(".test", StringComparison.OrdinalIgnoreCase))
            {
                testName = testName[..^5];
            }

            yield return new TestCaseData(file)
                .SetName(testName)
                .SetDescription($"Fixture: {relativeName}");
        }
    }
}

public sealed class TempFile : IDisposable
{
    public string DirectoryPath { get; }
    public string FilePath { get; }

    public TempFile(string directoryPath, string filePath)
    {
        DirectoryPath = directoryPath;
        FilePath = filePath;
    }

    public static implicit operator string(TempFile tf) => tf.FilePath;

    public override string ToString() => FilePath;

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }
}

public sealed class TempWorkspace : IDisposable
{
    public string WorkspacePath { get; }
    public string DirectoryPath => WorkspacePath;

    public TempWorkspace(string workspacePath)
    {
        WorkspacePath = workspacePath;
    }

    public string GetFilePath(string relativePath) =>
        Path.Combine(WorkspacePath, relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar));

    public static implicit operator string(TempWorkspace ws) => ws.WorkspacePath;

    public override string ToString() => WorkspacePath;

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(WorkspacePath))
            {
                Directory.Delete(WorkspacePath, true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }
}

