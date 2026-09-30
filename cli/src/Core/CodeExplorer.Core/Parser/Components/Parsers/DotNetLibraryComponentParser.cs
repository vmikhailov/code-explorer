namespace CodeExplorer.Core.Parser.Components.Parsers;

/// <summary>
/// Component parser for .NET Class Libraries (producing assembly/nupkg without web SDK).
/// </summary>
public class DotNetLibraryComponentParser : IComponentLibraryParser
{
    public string Id => "dotnet-library";
    public string Name => ".NET Class Library";

    public bool CanHandle(ProjectContext context)
    {
        if (!context.ProjectType.Equals("csharp", StringComparison.OrdinalIgnoreCase)) return false;

        var sdk = context.ManifestProperties.GetValueOrDefault("sdk");
        if (sdk?.Equals("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase) == true) return false;

        var outputType = context.ManifestProperties.GetValueOrDefault("output_type");
        var manifestType = context.ManifestProperties.GetValueOrDefault("manifest_type")?.ToLowerInvariant();

        return (outputType?.Equals("Library", StringComparison.OrdinalIgnoreCase) == true || manifestType == "library") &&
               context.ManifestProperties.GetValueOrDefault("is_test_project") != "true";
    }

    public ComponentAnalysisResult AnalyzeManifest(ProjectContext context)
    {
        return new ComponentAnalysisResult
        {
            ComponentId = Id,
            ComponentName = Name,
            Role = LibraryRole.SharedLibrary,
            Capabilities = ComponentCapabilities.SharedLibrary,
            Metadata = new Dictionary<string, string>
            {
                ["is_library"] = "true"
            }
        };
    }
}
