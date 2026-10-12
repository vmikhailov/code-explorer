using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Parser;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class Layer1Layer2MatrixBaselineTests
{
    private string _sampleProjectsRoot = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        WorkspaceIndexer.AutoRegisterParsers();

        // Resolve absolute path to cli/tests/SampleProjects
        var currentDir = TestContext.CurrentContext.TestDirectory;
        var dir = new DirectoryInfo(currentDir);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "cli", "tests", "SampleProjects");
            if (Directory.Exists(candidate))
            {
                _sampleProjectsRoot = candidate;
                break;
            }
            var direct = Path.Combine(dir.FullName, "SampleProjects");
            if (Directory.Exists(direct))
            {
                _sampleProjectsRoot = direct;
                break;
            }
            dir = dir.Parent;
        }

        Directory.Exists(_sampleProjectsRoot).Should().BeTrue($"SampleProjects directory not found. Probed upwards from: {TestContext.CurrentContext.TestDirectory}");
    }

    public record ExpectedProjectAnswer(
        [property: JsonPropertyName("projectType")] string ProjectType,
        [property: JsonPropertyName("entityKind")] ProjectEntityKind EntityKind,
        [property: JsonPropertyName("isLibrary")] bool IsLibrary
    );

    #region Full Workspace Scan & Expected Answers Comparison

    [Test]
    public async Task SampleProjects_FullWorkspaceScan_AllProjectsMatchExpectedAnswersJson()
    {
        var answersPath = Path.Combine(_sampleProjectsRoot, "expected.json");
        File.Exists(answersPath).Should().BeTrue($"Expected answers file not found at: {answersPath}");

        var json = await File.ReadAllTextAsync(answersPath);
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
        var expectedAnswers = JsonSerializer.Deserialize<Dictionary<string, ExpectedProjectAnswer>>(json, jsonOptions);
        expectedAnswers.Should().NotBeNull("Failed to deserialize expected.json");

        await using var client = new InMemoryGraphClient();
        var indexer = new WorkspaceIndexer(client);
        await indexer.IndexAsync(_sampleProjectsRoot, clear: false);

        var discoveredProjects = client.UploadedNodes
            .Where(n => n.Kind == OntologyConstants.NodeLabels.Project)
            .DistinctBy(n => n.Properties.GetValueOrDefault("path")?.ToString())
            .ToDictionary(
                n => n.Properties.GetValueOrDefault("path")!.ToString()!.Replace('\\', '/').Trim('/'),
                n => n,
                StringComparer.OrdinalIgnoreCase);

        // 1. If any discovered project has no right answer in expected.json, fail with actionable snippet
        var unrecordedProjects = discoveredProjects.Keys
            .Where(k => !expectedAnswers!.ContainsKey(k))
            .OrderBy(k => k)
            .ToList();

        if (unrecordedProjects.Count > 0)
        {
            var snippets = unrecordedProjects.Select(k =>
            {
                var n = discoveredProjects[k];
                var type = n.Properties.GetValueOrDefault("project_type");
                var kind = n.Properties.GetValueOrDefault("entity_kind");
                var isLib = n.Properties.GetValueOrDefault("is_library");
                return $"  \"{k}\": {{\n    \"projectType\": \"{type}\",\n    \"entityKind\": \"{kind}\",\n    \"isLibrary\": {isLib}\n  }}";
            });

            Assert.Fail(
                $"Discovered {unrecordedProjects.Count} project folder(s) with no right answer in expected.json:\n\n" +
                string.Join(",\n", snippets) + "\n\n" +
                $"These projects are new and must be added manually (or via agent) to: {answersPath}");
        }

        // 2. If any expected project in expected.json was not discovered on disk, fail
        var missingFromScan = expectedAnswers!.Keys
            .Where(k => !discoveredProjects.ContainsKey(k))
            .OrderBy(k => k)
            .ToList();

        if (missingFromScan.Count > 0)
        {
            Assert.Fail(
                $"The following {missingFromScan.Count} project(s) defined in expected.json were not discovered on disk during the workspace scan:\n" +
                string.Join("\n", missingFromScan.Select(k => $"  - {k}")) + "\n\n" +
                "Please verify the project folders or clean up expected.json.");
        }

        // 3. Compare each discovered project against right answers in expected.json
        foreach (var (path, expected) in expectedAnswers)
        {
            var node = discoveredProjects[path];
            var actualType = node.Properties.GetValueOrDefault("project_type")?.ToString();
            var actualKindStr = node.Properties.GetValueOrDefault("entity_kind")?.ToString() ?? "";
            Enum.TryParse<ProjectEntityKind>(actualKindStr, true, out var actualKind);
            var actualIsLib = bool.TryParse(node.Properties.GetValueOrDefault("is_library")?.ToString(), out var b) && b;

            actualType.Should().Be(expected.ProjectType, $"Project '{path}' projectType mismatch");
            actualKind.Should().Be(expected.EntityKind, $"Project '{path}' entityKind mismatch");
            actualIsLib.Should().Be(expected.IsLibrary, $"Project '{path}' isLibrary mismatch");
        }
    }

    #endregion

    #region Invariant Guard: All Parsers Must Be Covered

    [Test]
    public async Task AllParsers_MustBeCoveredByBaselineMatrixTests()
    {
        // 1. Discover all parsers via reflection
        var registeredParsers = WorkspaceIndexer.AutoRegisterParsers();
        registeredParsers.Should().NotBeEmpty();

        // 2. Collect all project types from registered IProjectParser implementations
        var projectParsers = WorkspaceIndexer.GetAllProjectParsers();
        var discoveredProjectTypes = projectParsers
            .Select(p => p.ProjectType.ToLowerInvariant())
            .Distinct()
            .Where(t => t != "sql" && t != "javascript") // SQL is embedded, JavaScript is handled by TypeScriptParser
            .ToList();

        // 3. Extract all tested project types from expected.json
        var answersPath = Path.Combine(_sampleProjectsRoot, "expected.json");
        File.Exists(answersPath).Should().BeTrue();
        var json = await File.ReadAllTextAsync(answersPath);
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
        var expectedAnswers = JsonSerializer.Deserialize<Dictionary<string, ExpectedProjectAnswer>>(json, jsonOptions)!;

        var testedProjectTypes = expectedAnswers.Values
            .Select(a => a.ProjectType.ToLowerInvariant())
            .Distinct()
            .ToHashSet();

        // 4. Assert every discovered project parser is tested in expected.json
        var untested = discoveredProjectTypes.Where(t => !testedProjectTypes.Contains(t)).ToList();
        untested.Should().BeEmpty(
            $"Every project parser must have sample projects and coverage in expected.json! " +
            $"Untested project types: [{string.Join(", ", untested)}]");

        // 5. Assert all IFileParser implementations are registered
        var fileParsers = WorkspaceIndexer.GetAllFileParsers();
        fileParsers.Should().NotBeEmpty();

        // 6. Assert all sample project paths in expected.json actually exist on disk
        foreach (var relPath in expectedAnswers.Keys)
        {
            var absPath = Path.Combine(_sampleProjectsRoot, relPath);
            Directory.Exists(absPath).Should().BeTrue($"Sample project directory must exist on disk: {relPath}");
        }
    }

    #endregion
}
