using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Semantics;

/// <summary>
/// Semantic categorization of parameters, variables, and arguments inferred from API sinks or AST provenance.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ParameterNature>))]
public enum ParameterNature : byte
{
    Unspecified = 0,
    SqlQuery = 1,
    UrlOrRoute = 2,
    ConnectionString = 3,
    FilePath = 4,
    CommandOrScript = 5,
    TopicOrQueue = 6,
    SecretOrHeader = 7,
    Config = 8,
    GraphQuery = 9,
    JsonPayload = 10,
    RegexPattern = 11
}

/// <summary>
/// Origin provenance of an argument passed into a semantic sink or wrapper.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ArgumentProvenance>))]
public enum ArgumentProvenance : byte
{
    Unknown = 0,
    InternalConstant = 1,
    MemberOrField = 2,
    ConfigOrEnv = 3,
    CallerParameter = 4,
    ExternalReturn = 5,
    DynamicComposite = 6
}

/// <summary>
/// Structural role of an argument or root origin inside a composite expression.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ExpressionRole>))]
public enum ExpressionRole : byte
{
    Standalone = 0,
    CompositeBase = 1,
    CompositeSuffix = 2,
    InterpolatedTemplate = 3,
    FallbackDefault = 4
}

/// <summary>
/// Formal ontology of the compile-time value sitting inside a resolved semantic argument.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ConstantValueKind>))]
public enum ConstantValueKind : byte
{
    Unknown = 0,
    SqlDml = 1,
    SqlDdl = 2,
    SqlExec = 3,
    UrlRelativeRoute = 4,
    UrlPublicDomain = 5,
    UrlLoopback = 6,
    ConnStrRelational = 7,
    SecretLiteral = 8,
    TopicLiteral = 9,
    FilePathLiteral = 10,
    CommandLiteral = 11
}
