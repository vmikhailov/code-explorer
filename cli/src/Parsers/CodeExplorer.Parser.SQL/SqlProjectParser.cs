[assembly: ParserAssembly]

namespace CodeExplorer.Parser.SQL;

[ProjectParser(Tier = ParserTier.Data)]
public class SqlProjectParser : IProjectParser
{
    public string ProjectType => "sql";

    public IReadOnlyCollection<string> ExcludedFolders => [];

    public IReadOnlyList<PackageDescriptor> Packages => [];

    public bool IsProjectDirectory(string directoryPath, string[] filesInDirectory) => false;

    public Task<ProducedPackageInfo?> GetProducedPackageAsync(string projectDirectory) =>
        Task.FromResult<ProducedPackageInfo?>(null);

    public Task<ProjectDependencyInfo> ParseDependenciesAsync(string projectDirectory) =>
        Task.FromResult(new ProjectDependencyInfo(new List<string>(), new List<ProducedPackageInfo>()));
}
