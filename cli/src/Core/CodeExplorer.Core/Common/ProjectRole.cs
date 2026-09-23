namespace CodeExplorer.Core.Common;

/// <summary>
/// Architectural role of a project or module within the system (C4 Container / Component level typing).
/// </summary>
public enum ProjectRole
{
    /// <summary>
    /// Executable backend service or API daemon (e.g. ASP.NET Core Web API, NestJS, Express, Go HTTP server).
    /// </summary>
    Service,

    /// <summary>
    /// Shared library, utility module, DTO package, or domain contract reused across services.
    /// </summary>
    SharedLibrary,

    /// <summary>
    /// Single-page application, web frontend, or mobile client (e.g. React, Vue, Next.js, Angular).
    /// </summary>
    FrontendApp,

    /// <summary>
    /// Background job processor, queue consumer, or scheduled task (e.g. Hangfire, Celery, Worker Service).
    /// </summary>
    Worker,

    /// <summary>
    /// Database schema migration runner or SQL script bundle (e.g. Flyway, Liquibase, EF Core migrations).
    /// </summary>
    DatabaseMigration,

    /// <summary>
    /// Command-line tool, developer script, or administrative CLI utility.
    /// </summary>
    CliTool,

    /// <summary>
    /// Unit, integration, or end-to-end test suite (e.g. NUnit, xUnit, Jest, Vitest, Playwright).
    /// </summary>
    Test
}
