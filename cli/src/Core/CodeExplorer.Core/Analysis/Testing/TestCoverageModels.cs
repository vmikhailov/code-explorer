namespace CodeExplorer.Core.Analysis.Testing;

public record TestCoverageFilter(
    string? Project = null,
    string? PathPrefix = null,
    string? Status = null, // "all", "covered", "uncovered"
    int? Limit = null
);

public record CoverageSummary(
    int TotalClasses,
    int CoveredClasses,
    int UncoveredClasses,
    double ClassCoveragePercentage,
    int TotalMethods,
    int CoveredMethods,
    int UncoveredMethods,
    double MethodCoveragePercentage
);

public record CoveredClassInfo(
    string Name,
    string Symbol,
    string FilePath,
    string? Project,
    int CoveredMethodsCount,
    int TotalMethodsCount,
    double MethodCoveragePercentage,
    int TotalCoveringTests
);

public record UncoveredClassInfo(
    string Name,
    string Symbol,
    string FilePath,
    int StartLine,
    int EndLine,
    string? Project,
    int MethodCount
);

public record CoveredMethodInfo(
    string Name,
    string Symbol,
    string? ClassName,
    string FilePath,
    int StartLine,
    int EndLine,
    string? Project,
    int MinCallDepth,
    IReadOnlyList<string> CoveringTests
);

public record UncoveredMethodInfo(
    string Name,
    string Symbol,
    string? ClassName,
    string FilePath,
    int StartLine,
    int EndLine,
    string? Project
);

public record TestCoverageReport(
    CoverageSummary Summary,
    IReadOnlyList<CoveredClassInfo> CoveredClasses,
    IReadOnlyList<UncoveredClassInfo> UncoveredClasses,
    IReadOnlyList<CoveredMethodInfo> CoveredMethods,
    IReadOnlyList<UncoveredMethodInfo> UncoveredMethods
);
