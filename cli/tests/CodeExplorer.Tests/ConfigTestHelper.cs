using CodeExplorer.Core.Parser;

namespace CodeExplorer.Tests;

internal static class ConfigTestHelper
{
    /// <summary>Parses the file once through the shared parsers and registers the result, as the pipeline does.</summary>
    public static void Load(string filePath, string? projectName)
    {
        foreach (var info in SharedFileParserRegistry.ParseFile(filePath).OfType<ConfigurationFileInformation>())
        {
            ConfigurationIndex.Register(info, projectName);
        }
    }
}
