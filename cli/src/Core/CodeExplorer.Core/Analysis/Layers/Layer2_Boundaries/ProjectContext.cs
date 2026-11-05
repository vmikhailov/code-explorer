using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Parser;

/// <summary>
/// Language-agnostic context describing a project boundary and its declared properties
/// for pluggable dialect-based role and entity classification.
/// </summary>
public sealed record ProjectContext(
    string DirectoryPath,
    string RelativeProjectDir,
    string ProjectName,
    ProjectLanguage ProjectType,
    IReadOnlyList<string> FilesInDirectory,
    IReadOnlyList<string> Dependencies,
    IReadOnlyDictionary<string, string> ManifestProperties
)
{
    public ProjectContext(
        string DirectoryPath,
        string RelativeProjectDir,
        string ProjectName,
        string ProjectType,
        IReadOnlyList<string> FilesInDirectory,
        IReadOnlyList<string> Dependencies,
        IReadOnlyDictionary<string, string> ManifestProperties)
        : this(DirectoryPath, RelativeProjectDir, ProjectName, TaxonomyEnumExtendedExtensions.ParseProjectLanguage(ProjectType), FilesInDirectory, Dependencies, ManifestProperties)
    {
    }
}
