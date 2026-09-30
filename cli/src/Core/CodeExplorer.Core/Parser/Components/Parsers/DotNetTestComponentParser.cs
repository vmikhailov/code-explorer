namespace CodeExplorer.Core.Parser.Components.Parsers;

/// <summary>
/// Component parser for .NET test projects (xUnit, NUnit, MSTest).
/// </summary>
public class DotNetTestComponentParser : IComponentLibraryParser
{
    public string Id => "dotnet-test";
    public string Name => ".NET Test Framework";

    public bool CanHandle(ProjectContext context)
    {
        return context.Dependencies.Any(d =>
            d.StartsWith("xunit", StringComparison.OrdinalIgnoreCase) ||
            d.StartsWith("NUnit", StringComparison.OrdinalIgnoreCase) ||
            d.StartsWith("MSTest", StringComparison.OrdinalIgnoreCase) ||
            d.Equals("Microsoft.NET.Test.Sdk", StringComparison.OrdinalIgnoreCase)) ||
            context.ManifestProperties.GetValueOrDefault("is_test_project") == "true" ||
            context.ManifestProperties.GetValueOrDefault("manifest_type") == "test";
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
