namespace CodeExplorer.Core.Parser;

/// <summary>
/// A file in a project (or workspace root) handed to shared file parsers.
/// The content is read exactly once by the pipeline and reused by every matching parser.
/// </summary>
public sealed record SharedFileContext(string FullPath, string FileName, string Content);

/// <summary>
/// Structured information extracted from a project file that is not source code
/// (environment files, Dockerfiles, application settings, ...) and attached to the owning project.
/// </summary>
public interface IProjectEnrichment
{
    string SourceFilePath { get; }
}

/// <summary>
/// Language-agnostic parser for non-source files that may belong to any project type.
/// Implementations are auto-discovered, indexed by <see cref="Extensions"/> / <see cref="FileNamePrefixes"/>,
/// and several parsers may handle the same extension.
/// </summary>
public interface ISharedFileParser
{
    string Name { get; }

    /// <summary>Lower-case file extensions including the dot (e.g. ".env", ".json").</summary>
    IReadOnlyCollection<string> Extensions { get; }

    /// <summary>
    /// Lower-case file name prefixes for files whose extension is not meaningful (e.g. ".env" for ".env.local").
    /// </summary>
    IReadOnlyCollection<string> FileNamePrefixes => [];

    /// <summary>Refines the extension / prefix match using the full file name.</summary>
    bool CanParse(string fileName) => true;

    /// <summary>Parses the already-read content. Returns null when the file yields nothing.</summary>
    IProjectEnrichment? Parse(SharedFileContext file);
}
