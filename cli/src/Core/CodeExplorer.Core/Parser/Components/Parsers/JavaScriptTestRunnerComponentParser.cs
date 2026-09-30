namespace CodeExplorer.Core.Parser.Components.Parsers;

/// <summary>
/// Component parser for JavaScript/TypeScript test runners and frameworks (Jest, Vitest, Mocha, Cypress, Playwright).
/// </summary>
public class JavaScriptTestRunnerComponentParser : IComponentLibraryParser
{
    public string Id => "js-test-runner";
    public string Name => "JavaScript Test Runner";

    private static readonly HashSet<string> TestFrameworks = new(StringComparer.OrdinalIgnoreCase)
    {
        "jest", "@types/jest", "ts-jest", "babel-jest",
        "vitest", "@vitest/ui",
        "mocha", "@types/mocha",
        "cypress",
        "@playwright/test", "playwright",
        "jasmine", "supertest"
    };

    public bool CanHandle(ProjectContext context)
    {
        return context.Dependencies.Any(d => TestFrameworks.Contains(d)) ||
               context.ManifestProperties.GetValueOrDefault("manifest_type") == "test" ||
               context.ManifestProperties.GetValueOrDefault("is_test_project") == "true";
    }

    public ComponentAnalysisResult AnalyzeManifest(ProjectContext context)
    {
        return new ComponentAnalysisResult
        {
            ComponentId = Id,
            ComponentName = Name,
            Role = LibraryRole.TestFramework,
            Capabilities = ComponentCapabilities.TestRunner,
            Metadata = new Dictionary<string, string>
            {
                ["test_runner"] = "true"
            }
        };
    }
}
