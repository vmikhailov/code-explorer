namespace CodeExplorer.Core.Parser;

/// <summary>
/// Language-agnostic context describing a project boundary and its declared properties
/// for pluggable dialect-based role and entity classification.
/// </summary>
public sealed record ProjectContext(
    string DirectoryPath,
    string RelativeProjectDir,
    string ProjectName,
    string ProjectType,
    IReadOnlyList<string> FilesInDirectory,
    IReadOnlyList<string> Dependencies,
    IReadOnlyDictionary<string, string> ManifestProperties
);
