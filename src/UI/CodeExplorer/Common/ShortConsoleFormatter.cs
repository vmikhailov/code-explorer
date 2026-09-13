using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace CodeExplorer.Common;

public sealed class ShortConsoleFormatter : ConsoleFormatter
{
    public const string FormatterName = "short-console";

    public ShortConsoleFormatter() : base(FormatterName) { }

    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter textWriter)
    {
        var message = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception);
        if (message == null && logEntry.Exception == null)
        {
            return;
        }

        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var category = logEntry.Category ?? string.Empty;
        var dotIndex = category.LastIndexOf('.');
        var shortCategory = dotIndex >= 0 ? category[(dotIndex + 1)..] : category;

        var logLevelString = GetLogLevelString(logEntry.LogLevel);

        textWriter.Write($"[{timestamp}] ");
        textWriter.Write($"{logLevelString}: ");
        textWriter.Write($"{shortCategory}[{logEntry.EventId.Id}] ");
        textWriter.WriteLine(message);

        if (logEntry.Exception != null)
        {
            textWriter.WriteLine(logEntry.Exception.ToString());
        }
    }

    private static string GetLogLevelString(LogLevel logLevel) => logLevel switch
    {
        LogLevel.Trace => "trce",
        LogLevel.Debug => "dbug",
        LogLevel.Information => "info",
        LogLevel.Warning => "warn",
        LogLevel.Error => "fail",
        LogLevel.Critical => "crit",
        _ => "none"
    };
}

public static class ShortConsoleLoggingExtensions
{
    public static ILoggingBuilder AddShortConsole(this ILoggingBuilder builder, Action<ConsoleLoggerOptions>? configure = null)
    {
        return builder
            .AddConsole(options =>
            {
                options.FormatterName = ShortConsoleFormatter.FormatterName;
                configure?.Invoke(options);
            })
            .AddConsoleFormatter<ShortConsoleFormatter, ConsoleFormatterOptions>();
    }
}
