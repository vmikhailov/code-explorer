using CodeExplorer.Core.Common;

namespace CodeExplorer.Core.Analysis;

/// <summary>
/// Pluggable classifier for determining a project's physical entity kind (Library, Service, Worker, etc.)
/// based on concrete manifest and syntactic evidence without relying on customer-specific naming hacks.
/// </summary>
public interface IProjectEntityClassifier
{
    /// <summary>
    /// Execution order (lower runs first).
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Attempts to classify the project entity based on concrete evidence.
    /// Returns null if this classifier does not have sufficient evidence.
    /// </summary>
    ProjectClassification? Classify(
        string directoryPath,
        string[] filesInDirectory,
        string relativeProjectDir,
        string projectName,
        string projectType,
        IReadOnlyList<string>? dependencies,
        IReadOnlyDictionary<string, string>? extensions);
}
