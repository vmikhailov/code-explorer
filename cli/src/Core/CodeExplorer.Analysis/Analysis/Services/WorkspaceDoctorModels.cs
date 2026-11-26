namespace CodeExplorer.Core.Analysis;

public sealed class WorkspaceDoctorOptions
{
    public bool IncludeIssues { get; set; } = true;
    public bool IncludeGaps { get; set; } = true;
    public bool Anonymize { get; set; }
    public int MaxSamples { get; set; } = 25;
    public int QueryTimeoutSeconds { get; set; } = 60;
}

public sealed class WorkspaceDoctorReport
{
    public string CodeExplorerVersion { get; set; } = string.Empty;
    public bool Anonymized { get; set; }
    public DoctorHealthSummary Health { get; set; } = new();
    public DoctorIssuesSummary? Issues { get; set; }
    public DoctorGapsSummary? Gaps { get; set; }
}

public sealed class DoctorHealthSummary
{
    public int SchemaVersion { get; set; }
    public int CurrentSchemaVersion { get; set; }
    public bool IsSchemaOutdated { get; set; }
    public long TotalNodes { get; set; }
    public long TotalRelationships { get; set; }
    public int ProjectCount { get; set; }
    public List<string> LanguagesDetected { get; set; } = [];
    public Dictionary<string, int> KeyNodeCounts { get; set; } = [];
}

public sealed class DoctorIssuesSummary
{
    public int TotalCount { get; set; }
    public List<DoctorIssueGroup> Groups { get; set; } = [];
    public List<DoctorIssueSample> Samples { get; set; } = [];
}

public enum DoctorAnomalyKind
{
    MissingEndpoints,
    MissingDatabaseLineage,
    MissingTopics,
    ZeroFunctions
}

public sealed record DoctorIssueGroup(
    string Stage,
    string IssueCode,
    string Language,
    string Severity,
    int Count);

public sealed record DoctorIssueSample(
    string Path,
    int Line,
    string Stage,
    string IssueCode,
    string Severity,
    string Language,
    string Message);

public sealed class DoctorGapsSummary
{
    public List<DoctorExtensionGap> UnhandledExtensions { get; set; } = [];
    public List<DoctorXmlDialectGap> UnhandledXmlDialects { get; set; } = [];
    public List<DoctorUnmatchedPackage> UnmatchedPackages { get; set; } = [];
    public int OtherUnmatchedPackageCount { get; set; }
    public List<DoctorCoverageAnomaly> CoverageAnomalies { get; set; } = [];
}

public sealed record DoctorExtensionGap(
    string Extension,
    int FileCount);

public sealed record DoctorXmlDialectGap(
    string RootElement,
    string NamespaceUri,
    int FileCount);

public sealed record DoctorUnmatchedPackage(
    string Name,
    string Ecosystem,
    string Role);

public sealed record DoctorCoverageAnomaly(
    string ProjectLabel,
    string Language,
    DoctorAnomalyKind Kind,
    string Detail);
