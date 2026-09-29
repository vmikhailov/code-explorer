using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common;

/// <summary>
/// Secondary sub-kind categorization for client applications (App) and specialized project runtimes.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProjectEntitySubKind
{
    None = 0,

    /// <summary>
    /// Web application (SPA, SSR, React, Angular, Vue, Next.js, etc.).
    /// </summary>
    Web,

    /// <summary>
    /// Mobile application (iOS, Android, React Native, Flutter, MAUI).
    /// </summary>
    Mobile,

    /// <summary>
    /// Desktop client application (Electron, WPF, WinUI, Avalonia, Qt).
    /// </summary>
    Desktop,

    /// <summary>
    /// Command-line tool, console app, or administrative CLI utility.
    /// </summary>
    Cli
}
