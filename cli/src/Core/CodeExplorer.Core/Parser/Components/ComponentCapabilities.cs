namespace CodeExplorer.Core.Parser.Components;

/// <summary>
/// Bitwise capability flags discovered by component/library parsers.
/// Multiple libraries in a project accumulate their capabilities into a composite profile.
/// </summary>
[Flags]
public enum ComponentCapabilities
{
    None = 0,
    UiLibrary = 1 << 0,
    FrontendApp = 1 << 1,
    HttpEndpoints = 1 << 2,
    Scheduler = 1 << 3,
    QueueWorker = 1 << 4,
    ApiGateway = 1 << 5,
    TestRunner = 1 << 6,
    DatabaseAccess = 1 << 7,
    EgressClient = 1 << 8,
    CliTool = 1 << 9,
    SharedLibrary = 1 << 10
}
