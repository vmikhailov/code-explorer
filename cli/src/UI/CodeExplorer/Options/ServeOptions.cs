using CommandLine;

namespace CodeExplorer.Options;

[Verb("serve", HelpText = "Starts the real-time WebSocket and HTTP API server for CodeExplorer.")]
public class ServeOptions
{
    [Option("port", Default = 0, HelpText = "Port to listen on (0 for automatically allocated ephemeral port).")]
    public int Port { get; set; } = 0;

    [Option("host", Default = "127.0.0.1", HelpText = "Host interface to bind to (default: 127.0.0.1).")]
    public string Host { get; set; } = "127.0.0.1";

    [Option("root", Required = false, HelpText = "Explicit workspace root directory.")]
    public string? Root { get; set; }

    [Option("db-path", Required = false, HelpText = "Explicit SQLite database path.")]
    public string? DbPath { get; set; }

    [Option("idle-timeout", Default = 30, HelpText = "Seconds of inactivity with 0 connected clients before automatic shutdown (0 to disable).")]
    public int IdleTimeoutSeconds { get; set; } = 30;

    [Option('q', "quiet", Required = false, HelpText = "Suppress console logging output.")]
    public bool Quiet { get; set; }
}
