namespace CodeExplorer.Core.Parser;

public record ProducedPackageInfo(string Name, string Version, string Ecosystem, string Scope = "runtime");
