namespace CodeExplorer.Core.Parser;

/// <summary>A single flattened configuration key (e.g. "Kafka:Topics:Orders" or "DATABASE_URL") and its value.</summary>
public sealed record ConfigValue(string Key, string Value);

/// <summary>Key/value content of an application configuration file (appsettings, yaml, properties, toml, web.config).</summary>
public record ConfigurationFileInformation(
    string SourceFilePath,
    string Format,
    IReadOnlyList<ConfigValue> Values) : IProjectEnrichment;

/// <summary>Variables declared by a dotenv file (.env, .env.local, dev.env, ...).</summary>
public sealed record EnvironmentInformation(
    string SourceFilePath,
    IReadOnlyList<ConfigValue> Values) : ConfigurationFileInformation(SourceFilePath, "dotenv", Values);
