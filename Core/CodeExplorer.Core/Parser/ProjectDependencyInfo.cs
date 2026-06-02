namespace CodeExplorer.Parser;

public record ProjectDependencyInfo(
    IReadOnlyCollection<string> LocalProjectPaths,
    IReadOnlyCollection<ProducedPackageInfo> ExternalPackages
);
