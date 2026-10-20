namespace CodeExplorer.Core.Parser;

public record ProjectDependencyInfo(
    IReadOnlyCollection<string> LocalProjectPaths,
    IReadOnlyCollection<ProducedPackageInfo> ExternalPackages
);
