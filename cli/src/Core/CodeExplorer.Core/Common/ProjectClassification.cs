namespace CodeExplorer.Core.Common;

/// <summary>
/// Architectural and physical classification of a project, combining primary entity kind and optional sub-kind.
/// </summary>
public record ProjectClassification(
    ProjectEntityKind Kind,
    ProjectEntitySubKind SubKind = ProjectEntitySubKind.None
)
{
    public static readonly ProjectClassification Unknown = new(ProjectEntityKind.Unknown);
    public static readonly ProjectClassification Library = new(ProjectEntityKind.Library);
    public static readonly ProjectClassification Service = new(ProjectEntityKind.Service);
    public static readonly ProjectClassification Worker = new(ProjectEntityKind.Worker);
    public static readonly ProjectClassification FunctionApp = new(ProjectEntityKind.FunctionApp);
    public static readonly ProjectClassification DatabaseMigration = new(ProjectEntityKind.DatabaseMigration);
    public static readonly ProjectClassification Test = new(ProjectEntityKind.Test);

    public static readonly ProjectClassification WebApp = new(ProjectEntityKind.App, ProjectEntitySubKind.Web);
    public static readonly ProjectClassification MobileApp = new(ProjectEntityKind.App, ProjectEntitySubKind.Mobile);
    public static readonly ProjectClassification DesktopApp = new(ProjectEntityKind.App, ProjectEntitySubKind.Desktop);
    public static readonly ProjectClassification CliApp = new(ProjectEntityKind.App, ProjectEntitySubKind.Cli);
}
