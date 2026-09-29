namespace CodeExplorer.Core.Common;

/// <summary>
/// Unambiguous classification of what a project or module is as a physical or build artifact.
/// Determined from build manifests, packaging, compiler configurations, and declared entry points.
/// </summary>
public enum ProjectEntityKind
{
    Unknown = 0,

    /// <summary>
    /// Reusable package, class library, shared module, or SDK without an independent daemon entrypoint.
    /// </summary>
    Library,

    /// <summary>
    /// Executable backend service, API daemon, or microservice.
    /// </summary>
    Service,

    /// <summary>
    /// Serverless function application (e.g. Azure Functions, AWS Lambda, Cloudflare Worker).
    /// </summary>
    FunctionApp,

    /// <summary>
    /// Client application (Web, Mobile, Desktop, Cli) differentiated by ProjectEntitySubKind.
    /// </summary>
    App,

    /// <summary>
    /// Background queue consumer, worker service, or batch job processor.
    /// </summary>
    Worker,

    /// <summary>
    /// Database schema migration runner or SQL script bundle.
    /// </summary>
    DatabaseMigration,

    /// <summary>
    /// Test suite (unit, integration, e2e, benchmark).
    /// </summary>
    Test
}
