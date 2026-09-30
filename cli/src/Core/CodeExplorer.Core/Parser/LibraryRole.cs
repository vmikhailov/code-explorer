namespace CodeExplorer.Core.Parser;

/// <summary>
/// Declares the architectural role of a library/framework when present in a project.
/// Used by pluggable dialect parsers to determine the project entity kind via Inversion of Control.
/// </summary>
public enum LibraryRole
{
    General,
    TestFramework,
    OrmOrDatabase,
    MessageBroker,
    CloudSdk,
    TelemetryAndLogging,
    AuthAndSecurity,
    Utility,
    WebFramework,
    WebService,
    FrontendFramework,
    MobileFramework,
    DesktopFramework,
    CliFramework,
    FunctionFramework,
    WorkerService,
    DatabaseMigration,
    UiComponentLibrary,
    Scheduler,
    ApiGateway,
    EgressClient,
    SharedLibrary
}
