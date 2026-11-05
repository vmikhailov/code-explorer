using System.Runtime.CompilerServices;

namespace CodeExplorer.Cypher.Common;

public static class TaxonomyEnumExtendedExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ToCanonicalString(this OperationType val) => val switch
    {
        OperationType.Query => "Query",
        OperationType.Mutation => "Mutation",
        OperationType.Subscription => "Subscription",
        OperationType.Unary => "Unary",
        OperationType.ClientStreaming => "ClientStreaming",
        OperationType.ServerStreaming => "ServerStreaming",
        OperationType.DuplexStreaming => "DuplexStreaming",
        OperationType.Command => "Command",
        OperationType.Event => "Event",
        OperationType.Read => "Read",
        OperationType.Write => "Write",
        OperationType.ReadWrite => "ReadWrite",
        OperationType.Orchestration => "Orchestration",
        OperationType.Validation => "Validation",
        OperationType.Transformation => "Transformation",
        OperationType.Configuration => "Configuration",
        OperationType.Rest => "Rest",
        OperationType.Rpc => "Rpc",
        OperationType.WebSocket => "WebSocket",
        OperationType.Compute => "Compute",
        OperationType.Auth => "Auth",
        OperationType.Integration => "Integration",
        OperationType.Notification => "Notification",
        OperationType.Scheduling => "Scheduling",
        OperationType.Lifecycle => "Lifecycle",
        OperationType.Testing => "Testing",
        OperationType.BidirectionalStreaming => "BidirectionalStreaming",
        OperationType.Grain => "Grain",
        OperationType.Wcf => "WCF",
        OperationType.Unknown => "Unknown",
        _ => ""
    };

    public static OperationType ParseOperationType(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return OperationType.Unspecified;
        return raw.Trim().ToLowerInvariant() switch
        {
            "query" => OperationType.Query,
            "mutation" => OperationType.Mutation,
            "subscription" => OperationType.Subscription,
            "unary" => OperationType.Unary,
            "clientstreaming" or "client_streaming" => OperationType.ClientStreaming,
            "serverstreaming" or "server_streaming" => OperationType.ServerStreaming,
            "bidirectionalstreaming" or "bidirectional_streaming" => OperationType.BidirectionalStreaming,
            "duplexstreaming" or "duplex_streaming" or "bidirectional" => OperationType.DuplexStreaming,
            "grain" => OperationType.Grain,
            "wcf" => OperationType.Wcf,
            "command" => OperationType.Command,
            "event" => OperationType.Event,
            "read" => OperationType.Read,
            "write" => OperationType.Write,
            "readwrite" or "read_write" or "crud" => OperationType.ReadWrite,
            "orchestration" => OperationType.Orchestration,
            "validation" => OperationType.Validation,
            "transformation" => OperationType.Transformation,
            "configuration" or "config" => OperationType.Configuration,
            "rest" => OperationType.Rest,
            "rpc" => OperationType.Rpc,
            "websocket" or "ws" => OperationType.WebSocket,
            "compute" => OperationType.Compute,
            "auth" or "authentication" or "authorization" => OperationType.Auth,
            "integration" => OperationType.Integration,
            "notification" => OperationType.Notification,
            "scheduling" => OperationType.Scheduling,
            "lifecycle" => OperationType.Lifecycle,
            "testing" or "test" => OperationType.Testing,
            _ => OperationType.Unknown
        };
    }

    public static OperationType? ParseNullableOperationType(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var parsed = ParseOperationType(raw);
        return parsed == OperationType.Unspecified ? null : parsed;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ToCanonicalString(this ScanIssueCategory val) => val switch
    {
        ScanIssueCategory.SyntaxError => "syntax_error",
        ScanIssueCategory.ParseException => "parse_exception",
        ScanIssueCategory.VisitorException => "visitor_exception",
        ScanIssueCategory.ManifestError => "manifest_error",
        ScanIssueCategory.IoError => "io_error",
        ScanIssueCategory.ReferenceError => "reference_error",
        ScanIssueCategory.SemanticError => "semantic_error",
        ScanIssueCategory.ConfigurationError => "configuration_error",
        _ => "syntax_error"
    };

    public static ScanIssueCategory ParseScanIssueCategory(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return ScanIssueCategory.SyntaxError;
        return raw.Trim().ToLowerInvariant() switch
        {
            "syntax_error" or "syntaxerror" => ScanIssueCategory.SyntaxError,
            "parse_exception" or "parseexception" => ScanIssueCategory.ParseException,
            "visitor_exception" or "visitorexception" => ScanIssueCategory.VisitorException,
            "manifest_error" or "manifesterror" => ScanIssueCategory.ManifestError,
            "io_error" or "ioerror" => ScanIssueCategory.IoError,
            "reference_error" or "referenceerror" => ScanIssueCategory.ReferenceError,
            "semantic_error" or "semanticerror" => ScanIssueCategory.SemanticError,
            "configuration_error" or "configurationerror" => ScanIssueCategory.ConfigurationError,
            _ => ScanIssueCategory.SyntaxError
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ToCanonicalString(this ProjectLanguage val) => val switch
    {
        ProjectLanguage.CSharp => "csharp",
        ProjectLanguage.TypeScript => "typescript",
        ProjectLanguage.JavaScript => "javascript",
        ProjectLanguage.Python => "python",
        ProjectLanguage.Go => "go",
        ProjectLanguage.Rust => "rust",
        ProjectLanguage.Java => "java",
        ProjectLanguage.Kotlin => "kotlin",
        ProjectLanguage.Sql => "sql",
        ProjectLanguage.ColdFusion => "coldfusion",
        ProjectLanguage.Script => "script",
        ProjectLanguage.C => "c",
        ProjectLanguage.Cpp => "cpp",
        ProjectLanguage.Ruby => "ruby",
        ProjectLanguage.Php => "php",
        ProjectLanguage.Swift => "swift",
        ProjectLanguage.Dart => "dart",
        ProjectLanguage.Scala => "scala",
        ProjectLanguage.Elixir => "elixir",
        ProjectLanguage.GraphQl => "graphql",
        ProjectLanguage.Protobuf => "protobuf",
        ProjectLanguage.Html => "html",
        ProjectLanguage.Css => "css",
        ProjectLanguage.Json => "json",
        ProjectLanguage.Yaml => "yaml",
        ProjectLanguage.Toml => "toml",
        ProjectLanguage.Xml => "xml",
        ProjectLanguage.Markdown => "markdown",
        ProjectLanguage.Dockerfile => "dockerfile",
        ProjectLanguage.Terraform => "terraform",
        ProjectLanguage.Batch => "batch",
        ProjectLanguage.Shell => "shell",
        ProjectLanguage.PowerShell => "powershell",
        ProjectLanguage.Android => "android",
        ProjectLanguage.MultiLanguage => "multilanguage",
        _ => "unknown"
    };

    public static ProjectLanguage ParseProjectLanguage(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return ProjectLanguage.Unknown;
        return raw.Trim().ToLowerInvariant() switch
        {
            "csharp" or "c#" or "c_sharp" or "c-sharp" or "dotnet" or ".net" => ProjectLanguage.CSharp,
            "typescript" or "ts" or "tsx" => ProjectLanguage.TypeScript,
            "javascript" or "js" or "jsx" or "node" or "nodejs" => ProjectLanguage.JavaScript,
            "python" or "py" => ProjectLanguage.Python,
            "go" or "golang" => ProjectLanguage.Go,
            "rust" or "rs" or "cargo" => ProjectLanguage.Rust,
            "java" or "maven" or "gradle" => ProjectLanguage.Java,
            "kotlin" or "kt" => ProjectLanguage.Kotlin,
            "sql" or "tsql" or "plsql" or "pgsql" => ProjectLanguage.Sql,
            "coldfusion" or "cfml" or "cfc" or "cfm" => ProjectLanguage.ColdFusion,
            "script" or "scripts" => ProjectLanguage.Script,
            "c" => ProjectLanguage.C,
            "cpp" or "c++" or "cxx" => ProjectLanguage.Cpp,
            "ruby" or "rb" => ProjectLanguage.Ruby,
            "php" => ProjectLanguage.Php,
            "swift" => ProjectLanguage.Swift,
            "dart" or "flutter" => ProjectLanguage.Dart,
            "scala" => ProjectLanguage.Scala,
            "elixir" or "ex" => ProjectLanguage.Elixir,
            "graphql" or "gql" => ProjectLanguage.GraphQl,
            "protobuf" or "proto" => ProjectLanguage.Protobuf,
            "html" => ProjectLanguage.Html,
            "css" or "scss" or "less" => ProjectLanguage.Css,
            "json" => ProjectLanguage.Json,
            "yaml" or "yml" => ProjectLanguage.Yaml,
            "toml" => ProjectLanguage.Toml,
            "xml" => ProjectLanguage.Xml,
            "markdown" or "md" => ProjectLanguage.Markdown,
            "dockerfile" or "docker" => ProjectLanguage.Dockerfile,
            "terraform" or "hcl" => ProjectLanguage.Terraform,
            "batch" or "bat" or "cmd" => ProjectLanguage.Batch,
            "shell" or "sh" or "bash" or "zsh" => ProjectLanguage.Shell,
            "powershell" or "pwsh" or "ps1" => ProjectLanguage.PowerShell,
            "android" => ProjectLanguage.Android,
            "multilanguage" or "multi" => ProjectLanguage.MultiLanguage,
            _ => ProjectLanguage.Unknown
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ToCanonicalString(this ProjectArchetype val) => val switch
    {
        ProjectArchetype.Unclassified => "Unclassified",
        ProjectArchetype.PureUtility => "PureUtility",
        ProjectArchetype.ClientUi => "ClientUi",
        ProjectArchetype.CliTool => "CliTool",
        ProjectArchetype.Framework => "Framework",
        ProjectArchetype.BackendService => "BackendService",
        ProjectArchetype.DatabaseDriver => "DatabaseDriver",
        ProjectArchetype.NativeAddon => "NativeAddon",
        ProjectArchetype.Service => "Service",
        ProjectArchetype.SharedLibrary => "SharedLibrary",
        ProjectArchetype.FrontendApp => "FrontendApp",
        ProjectArchetype.Worker => "Worker",
        ProjectArchetype.DatabaseMigration => "DatabaseMigration",
        ProjectArchetype.ScriptProject => "ScriptProject",
        ProjectArchetype.Test => "Test",
        ProjectArchetype.App => "App",
        ProjectArchetype.Library => "Library",
        _ => "Unclassified"
    };

    public static ProjectArchetype ParseProjectArchetype(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return ProjectArchetype.Unclassified;
        return raw.Trim().ToLowerInvariant() switch
        {
            "pureutility" => ProjectArchetype.PureUtility,
            "clientui" => ProjectArchetype.ClientUi,
            "clitool" or "cli" => ProjectArchetype.CliTool,
            "framework" => ProjectArchetype.Framework,
            "backendservice" => ProjectArchetype.BackendService,
            "databasedriver" => ProjectArchetype.DatabaseDriver,
            "nativeaddon" => ProjectArchetype.NativeAddon,
            "service" => ProjectArchetype.Service,
            "sharedlibrary" => ProjectArchetype.SharedLibrary,
            "frontendapp" => ProjectArchetype.FrontendApp,
            "worker" => ProjectArchetype.Worker,
            "databasemigration" => ProjectArchetype.DatabaseMigration,
            "scriptproject" => ProjectArchetype.ScriptProject,
            "test" => ProjectArchetype.Test,
            "app" => ProjectArchetype.App,
            "library" => ProjectArchetype.Library,
            _ => ProjectArchetype.Unclassified
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ToCanonicalString(this SyntacticSymbolKind val) => val switch
    {
        SyntacticSymbolKind.File => "file",
        SyntacticSymbolKind.Type => "Type",
        SyntacticSymbolKind.Class => "Class",
        SyntacticSymbolKind.Interface => "Interface",
        SyntacticSymbolKind.Struct => "Struct",
        SyntacticSymbolKind.Record => "Record",
        SyntacticSymbolKind.Enum => "Enum",
        SyntacticSymbolKind.Delegate => "Delegate",
        SyntacticSymbolKind.Trait => "Trait",
        SyntacticSymbolKind.Union => "Union",
        SyntacticSymbolKind.Object => "Object",
        SyntacticSymbolKind.Module => "Module",
        SyntacticSymbolKind.Namespace => "Namespace",
        SyntacticSymbolKind.Package => "Package",
        SyntacticSymbolKind.Function => "Function",
        SyntacticSymbolKind.Method => "Method",
        SyntacticSymbolKind.Constructor => "Constructor",
        SyntacticSymbolKind.Member => "Member",
        SyntacticSymbolKind.Property => "Property",
        SyntacticSymbolKind.Field => "Field",
        SyntacticSymbolKind.Parameter => "Parameter",
        SyntacticSymbolKind.Variable => "Variable",
        SyntacticSymbolKind.Constant => "Constant",
        SyntacticSymbolKind.Event => "Event",
        SyntacticSymbolKind.EnumMember => "EnumMember",
        SyntacticSymbolKind.Endpoint => "Endpoint",
        SyntacticSymbolKind.EntryPoint => "EntryPoint",
        SyntacticSymbolKind.Table => "Table",
        SyntacticSymbolKind.Query => "Query",
        SyntacticSymbolKind.Procedure => "Procedure",
        SyntacticSymbolKind.Database => "Database",
        SyntacticSymbolKind.Topic => "Topic",
        SyntacticSymbolKind.ExternalService => "ExternalService",
        SyntacticSymbolKind.CloudService => "CloudService",
        SyntacticSymbolKind.CallLiteral => "CallLiteral",
        SyntacticSymbolKind.CatchClause => "CatchClause",
        SyntacticSymbolKind.Import => "Import",
        SyntacticSymbolKind.Export => "Export",
        SyntacticSymbolKind.Attribute => "Attribute",
        SyntacticSymbolKind.Component => "Component",
        SyntacticSymbolKind.Hook => "Hook",
        _ => "Unknown"
    };

    public static SyntacticSymbolKind ParseSyntacticSymbolKind(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return SyntacticSymbolKind.Unknown;
        return raw.Trim().ToLowerInvariant() switch
        {
            "file" => SyntacticSymbolKind.File,
            "type" => SyntacticSymbolKind.Type,
            "class" => SyntacticSymbolKind.Class,
            "interface" => SyntacticSymbolKind.Interface,
            "struct" => SyntacticSymbolKind.Struct,
            "record" => SyntacticSymbolKind.Record,
            "enum" => SyntacticSymbolKind.Enum,
            "delegate" => SyntacticSymbolKind.Delegate,
            "trait" => SyntacticSymbolKind.Trait,
            "union" => SyntacticSymbolKind.Union,
            "object" => SyntacticSymbolKind.Object,
            "module" => SyntacticSymbolKind.Module,
            "namespace" => SyntacticSymbolKind.Namespace,
            "package" => SyntacticSymbolKind.Package,
            "function" => SyntacticSymbolKind.Function,
            "method" => SyntacticSymbolKind.Method,
            "constructor" => SyntacticSymbolKind.Constructor,
            "member" => SyntacticSymbolKind.Member,
            "property" => SyntacticSymbolKind.Property,
            "field" => SyntacticSymbolKind.Field,
            "parameter" => SyntacticSymbolKind.Parameter,
            "variable" => SyntacticSymbolKind.Variable,
            "constant" => SyntacticSymbolKind.Constant,
            "event" => SyntacticSymbolKind.Event,
            "enummember" or "enum_member" => SyntacticSymbolKind.EnumMember,
            "endpoint" => SyntacticSymbolKind.Endpoint,
            "entrypoint" or "entry_point" => SyntacticSymbolKind.EntryPoint,
            "table" => SyntacticSymbolKind.Table,
            "query" => SyntacticSymbolKind.Query,
            "procedure" => SyntacticSymbolKind.Procedure,
            "database" => SyntacticSymbolKind.Database,
            "topic" => SyntacticSymbolKind.Topic,
            "externalservice" or "external_service" => SyntacticSymbolKind.ExternalService,
            "cloudservice" or "cloud_service" => SyntacticSymbolKind.CloudService,
            "callliteral" or "call_literal" => SyntacticSymbolKind.CallLiteral,
            "catchclause" or "catch_clause" => SyntacticSymbolKind.CatchClause,
            "import" => SyntacticSymbolKind.Import,
            "export" => SyntacticSymbolKind.Export,
            "attribute" => SyntacticSymbolKind.Attribute,
            "component" => SyntacticSymbolKind.Component,
            "hook" => SyntacticSymbolKind.Hook,
            _ => SyntacticSymbolKind.Unknown
        };
    }

    public static BranchKind ParseBranchKind(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return BranchKind.Unspecified;
        var lower = raw.Trim().ToLowerInvariant();
        if (lower.Contains("break")) return BranchKind.Break;
        if (lower.Contains("continue")) return BranchKind.Continue;
        if (lower.Contains("goto")) return BranchKind.Goto;
        if (lower.Contains("return")) return BranchKind.Return;
        if (lower.Contains("yield")) return BranchKind.Yield;
        if (lower.Contains("throw")) return BranchKind.Throw;
        return BranchKind.Unspecified;
    }

    public static string ToCanonicalString(this BranchKind val) => val switch
    {
        BranchKind.Break => "break",
        BranchKind.Continue => "continue",
        BranchKind.Goto => "goto",
        BranchKind.Return => "return",
        BranchKind.Yield => "yield",
        BranchKind.Throw => "throw",
        _ => ""
    };

    public static string ToCanonicalString(this SyntaxDiagnosticKind val) => val switch
    {
        SyntaxDiagnosticKind.SyntaxError => "syntax_error",
        SyntaxDiagnosticKind.MissingToken => "missing_token",
        SyntaxDiagnosticKind.Warning => "warning",
        _ => "syntax_error"
    };
}
