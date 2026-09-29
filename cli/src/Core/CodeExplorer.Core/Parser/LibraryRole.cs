namespace CodeExplorer.Core.Parser;

/// <summary>
/// Declares the architectural role of a library/framework when present in a project.
/// Used by pluggable dialect parsers to determine the project entity kind via Inversion of Control.
/// </summary>
public enum LibraryRole
{
    General,
    TestFramework,
    WebService,
    WorkerService,
    FrontendApp,
    CliTool,
    DatabaseMigration
}
