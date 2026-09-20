using NUnit.Framework;

namespace CodeExplorer.Cypher.Tests.Shared;

public static class CypherTestData
{
    public static IEnumerable<TestCaseData> GetFiles(string directory, string searchPattern = "*.cypher")
    {
        var baseDir = TestContext.CurrentContext.TestDirectory;
        var dirNormalized = directory.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var targetDir = Path.IsPathRooted(dirNormalized) ? dirNormalized : Path.Combine(baseDir, dirNormalized);

        if (!Directory.Exists(targetDir))
        {
            // Fallback: search upwards for solution root or test project directory
            var curr = new DirectoryInfo(baseDir);
            while (curr != null && !File.Exists(Path.Combine(curr.FullName, "CodeExplorer.slnx")))
            {
                curr = curr.Parent;
            }
            if (curr != null)
            {
                var candidate = Path.Combine(curr.FullName, "tests", "CodeExplorer.Cypher.Tests", dirNormalized);
                if (Directory.Exists(candidate))
                {
                    targetDir = candidate;
                }
            }
        }

        if (!Directory.Exists(targetDir))
        {
            throw new DirectoryNotFoundException($"Could not locate test queries directory: '{directory}' (checked '{targetDir}')");
        }

        var files = Directory.EnumerateFiles(targetDir, searchPattern, SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var relativeName = Path.GetRelativePath(targetDir, file).Replace('\\', '/');
            var testName = Path.GetFileNameWithoutExtension(file);

            yield return new TestCaseData(file)
                .SetName(testName)
                .SetDescription($"Cypher test case: {relativeName}");
        }
    }
}

