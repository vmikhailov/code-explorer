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
    /// Background queue consumer, worker service, or batch job processor.
    /// </summary>
    Worker,

    /// <summary>
    /// Serverless function (e.g. Azure Functions, AWS Lambda, Cloudflare Worker).
    /// </summary>
    Function,

    /// <summary>
    /// Web client application (SPA, SSR, React, Angular, Vue, Vite, Next.js, Nuxt).
    /// </summary>
    FrontendApp,

    /// <summary>
    /// Mobile client application (iOS, Android, React Native, Flutter, MAUI).
    /// </summary>
    MobileApp,

    /// <summary>
    /// Desktop client application (Electron, WPF, WinUI, Avalonia).
    /// </summary>
    DesktopApp,

    /// <summary>
    /// Command-line executable or administrative tool.
    /// </summary>
    CliTool,

    /// <summary>
    /// Database schema migration runner or SQL script bundle.
    /// </summary>
    MigrationTool,

    /// <summary>
    /// Infrastructure and deployment definitions (Terraform, Docker Compose, Helm).
    /// </summary>
    Resource,

    /// <summary>
    /// Test suite (unit, integration, e2e, benchmark).
    /// </summary>
    Test
}
