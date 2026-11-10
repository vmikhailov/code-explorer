using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeExplorer.Cypher.Common;

public static class TaxonomyViewEnumExtensions
{
    public static string ToCanonicalString(this AppType val) => val switch
    {
        AppType.Spa => "spa",
        AppType.Ssr => "ssr",
        AppType.Static => "static",
        AppType.Mobile => "mobile",
        AppType.Desktop => "desktop",
        AppType.Cli => "cli",
        AppType.Web => "web",
        AppType.Hybrid => "hybrid",
        _ => "web"
    };

    public static AppType ParseAppType(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return AppType.Unspecified;
        return raw.Trim().ToLowerInvariant() switch
        {
            "spa" => AppType.Spa,
            "ssr" => AppType.Ssr,
            "static" => AppType.Static,
            "mobile" => AppType.Mobile,
            "desktop" => AppType.Desktop,
            "cli" => AppType.Cli,
            "web" => AppType.Web,
            "hybrid" => AppType.Hybrid,
            _ => AppType.Web
        };
    }

    public static string ToCanonicalString(this ExternalServiceCategory val) => val switch
    {
        ExternalServiceCategory.Payment => "Payment",
        ExternalServiceCategory.Auth => "Auth",
        ExternalServiceCategory.Identity => "Identity",
        ExternalServiceCategory.Messaging => "Messaging",
        ExternalServiceCategory.Email => "Email",
        ExternalServiceCategory.Sms => "Sms",
        ExternalServiceCategory.Storage => "Storage",
        ExternalServiceCategory.Analytics => "Analytics",
        ExternalServiceCategory.Monitoring => "Monitoring",
        ExternalServiceCategory.Ai => "AI",
        ExternalServiceCategory.Search => "Search",
        ExternalServiceCategory.Crm => "CRM",
        ExternalServiceCategory.Social => "Social",
        ExternalServiceCategory.Cloud => "Cloud",
        ExternalServiceCategory.HttpApi => "HttpApi",
        ExternalServiceCategory.GrpcService => "GrpcService",
        ExternalServiceCategory.Webhook => "Webhook",
        ExternalServiceCategory.External => "External",
        _ => "General"
    };

    public static ExternalServiceCategory ParseExternalServiceCategory(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return ExternalServiceCategory.Unspecified;
        return raw.Trim().ToLowerInvariant() switch
        {
            "payment" or "payments" or "billing" => ExternalServiceCategory.Payment,
            "auth" or "authentication" or "oauth" => ExternalServiceCategory.Auth,
            "identity" or "iam" => ExternalServiceCategory.Identity,
            "messaging" or "chat" => ExternalServiceCategory.Messaging,
            "email" or "mail" => ExternalServiceCategory.Email,
            "sms" => ExternalServiceCategory.Sms,
            "storage" or "blob" => ExternalServiceCategory.Storage,
            "analytics" or "telemetry" => ExternalServiceCategory.Analytics,
            "monitoring" or "observability" => ExternalServiceCategory.Monitoring,
            "ai" or "llm" or "ml" => ExternalServiceCategory.Ai,
            "search" => ExternalServiceCategory.Search,
            "crm" => ExternalServiceCategory.Crm,
            "social" => ExternalServiceCategory.Social,
            "cloud" => ExternalServiceCategory.Cloud,
            "httpapi" or "http" or "rest" => ExternalServiceCategory.HttpApi,
            "grpcservice" or "grpc" => ExternalServiceCategory.GrpcService,
            "webhook" => ExternalServiceCategory.Webhook,
            "external" => ExternalServiceCategory.External,
            _ => ExternalServiceCategory.General
        };
    }

    public static string ToCanonicalString(this ProjectMapModuleType val) => val switch
    {
        ProjectMapModuleType.Module => "module",
        ProjectMapModuleType.SubModule => "submodule",
        ProjectMapModuleType.File => "file",
        ProjectMapModuleType.Class => "class",
        ProjectMapModuleType.EntryPoint => "entrypoint",
        ProjectMapModuleType.Endpoint => "endpoint",
        ProjectMapModuleType.SubProject => "subproject",
        ProjectMapModuleType.Resource => "resource",
        ProjectMapModuleType.Database => "database",
        _ => "module"
    };

    public static ProjectMapModuleType ParseProjectMapModuleType(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return ProjectMapModuleType.Module;
        return raw.Trim().ToLowerInvariant() switch
        {
            "module" => ProjectMapModuleType.Module,
            "submodule" or "sub_module" => ProjectMapModuleType.SubModule,
            "file" => ProjectMapModuleType.File,
            "class" or "type" => ProjectMapModuleType.Class,
            "entrypoint" or "entry_point" => ProjectMapModuleType.EntryPoint,
            "endpoint" => ProjectMapModuleType.Endpoint,
            "subproject" or "sub_project" => ProjectMapModuleType.SubProject,
            "resource" => ProjectMapModuleType.Resource,
            "database" => ProjectMapModuleType.Database,
            _ => ProjectMapModuleType.Module
        };
    }

    public static string ToCanonicalString(this BuiltInQueryCategory val) => val switch
    {
        BuiltInQueryCategory.Architecture => "Architecture",
        BuiltInQueryCategory.Refactoring => "Refactoring",
        BuiltInQueryCategory.DataLineage => "DataLineage",
        BuiltInQueryCategory.Security => "Security",
        BuiltInQueryCategory.Testing => "Testing",
        BuiltInQueryCategory.Symbols => "Symbols",
        BuiltInQueryCategory.Taxonomy => "Taxonomy",
        _ => "General"
    };

    public static BuiltInQueryCategory ParseBuiltInQueryCategory(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return BuiltInQueryCategory.General;
        return raw.Trim().ToLowerInvariant() switch
        {
            "architecture" => BuiltInQueryCategory.Architecture,
            "refactoring" => BuiltInQueryCategory.Refactoring,
            "datalineage" or "data_lineage" => BuiltInQueryCategory.DataLineage,
            "security" => BuiltInQueryCategory.Security,
            "testing" => BuiltInQueryCategory.Testing,
            "symbols" => BuiltInQueryCategory.Symbols,
            "taxonomy" => BuiltInQueryCategory.Taxonomy,
            _ => BuiltInQueryCategory.General
        };
    }

    public static string ToCanonicalString(this QueryParameterType val) => val switch
    {
        QueryParameterType.String => "string",
        QueryParameterType.Int => "int",
        QueryParameterType.Integer => "integer",
        QueryParameterType.Number => "number",
        QueryParameterType.Float => "float",
        QueryParameterType.Double => "double",
        QueryParameterType.Bool => "bool",
        QueryParameterType.Boolean => "boolean",
        QueryParameterType.Array => "array",
        QueryParameterType.Object => "object",
        _ => "string"
    };

    public static QueryParameterType ParseQueryParameterType(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return QueryParameterType.String;
        return raw.Trim().ToLowerInvariant() switch
        {
            "int" => QueryParameterType.Int,
            "integer" => QueryParameterType.Integer,
            "number" => QueryParameterType.Number,
            "float" => QueryParameterType.Float,
            "double" => QueryParameterType.Double,
            "bool" => QueryParameterType.Bool,
            "boolean" => QueryParameterType.Boolean,
            "array" or "list" => QueryParameterType.Array,
            "object" or "map" => QueryParameterType.Object,
            _ => QueryParameterType.String
        };
    }

    public static string ToCanonicalString(this BoundedContextInteractionType val) => val switch
    {
        BoundedContextInteractionType.Call => "call",
        BoundedContextInteractionType.Event => "event",
        BoundedContextInteractionType.Database => "database",
        BoundedContextInteractionType.Shared => "shared",
        BoundedContextInteractionType.Http => "http",
        BoundedContextInteractionType.Grpc => "grpc",
        BoundedContextInteractionType.Message => "message",
        BoundedContextInteractionType.Messaging => "messaging",
        BoundedContextInteractionType.ServiceCall => "service_call",
        _ => "call"
    };

    public static BoundedContextInteractionType ParseBoundedContextInteractionType(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return BoundedContextInteractionType.Call;
        return raw.Trim().ToLowerInvariant() switch
        {
            "event" => BoundedContextInteractionType.Event,
            "database" or "db" => BoundedContextInteractionType.Database,
            "shared" => BoundedContextInteractionType.Shared,
            "http" => BoundedContextInteractionType.Http,
            "grpc" => BoundedContextInteractionType.Grpc,
            "message" or "msg" => BoundedContextInteractionType.Message,
            "messaging" => BoundedContextInteractionType.Messaging,
            "service_call" or "servicecall" => BoundedContextInteractionType.ServiceCall,
            _ => BoundedContextInteractionType.Call
        };
    }

    public static string ToCanonicalString(this GraphEdgeCategory val) => val switch
    {
        GraphEdgeCategory.Architecture => "architecture",
        GraphEdgeCategory.Syntactic => "syntactic",
        GraphEdgeCategory.Semantic => "semantic",
        GraphEdgeCategory.DataFlow => "data_flow",
        GraphEdgeCategory.ControlFlow => "control_flow",
        GraphEdgeCategory.Dependency => "dependency",
        GraphEdgeCategory.Messaging => "messaging",
        GraphEdgeCategory.Database => "database",
        GraphEdgeCategory.Call => "call",
        GraphEdgeCategory.Containment => "containment",
        GraphEdgeCategory.Inheritance => "inheritance",
        GraphEdgeCategory.Testing => "testing",
        GraphEdgeCategory.Logical => "logical",
        GraphEdgeCategory.ServiceCall => "service_call",
        GraphEdgeCategory.Library => "library",
        GraphEdgeCategory.Resource => "resource",
        GraphEdgeCategory.Test => "test",
        GraphEdgeCategory.External => "external",
        GraphEdgeCategory.Configuration => "configuration",
        GraphEdgeCategory.UnidentifiedDependency => "unidentified_dependency",
        GraphEdgeCategory.Event => "event",
        GraphEdgeCategory.Shared => "shared",
        GraphEdgeCategory.Http => "http",
        GraphEdgeCategory.Grpc => "grpc",
        GraphEdgeCategory.Message => "message",
        _ => "semantic"
    };

    public static GraphEdgeCategory ParseGraphEdgeCategory(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return GraphEdgeCategory.Unspecified;
        return raw.Trim().ToLowerInvariant() switch
        {
            "architecture" => GraphEdgeCategory.Architecture,
            "syntactic" => GraphEdgeCategory.Syntactic,
            "semantic" => GraphEdgeCategory.Semantic,
            "data_flow" or "dataflow" => GraphEdgeCategory.DataFlow,
            "control_flow" or "controlflow" => GraphEdgeCategory.ControlFlow,
            "dependency" => GraphEdgeCategory.Dependency,
            "messaging" => GraphEdgeCategory.Messaging,
            "database" => GraphEdgeCategory.Database,
            "call" => GraphEdgeCategory.Call,
            "containment" => GraphEdgeCategory.Containment,
            "inheritance" => GraphEdgeCategory.Inheritance,
            "testing" => GraphEdgeCategory.Testing,
            "logical" => GraphEdgeCategory.Logical,
            "service_call" or "servicecall" => GraphEdgeCategory.ServiceCall,
            "library" => GraphEdgeCategory.Library,
            "resource" => GraphEdgeCategory.Resource,
            "test" => GraphEdgeCategory.Test,
            "external" => GraphEdgeCategory.External,
            "configuration" or "config" => GraphEdgeCategory.Configuration,
            "unidentified_dependency" => GraphEdgeCategory.UnidentifiedDependency,
            "event" => GraphEdgeCategory.Event,
            "shared" => GraphEdgeCategory.Shared,
            "http" => GraphEdgeCategory.Http,
            "grpc" => GraphEdgeCategory.Grpc,
            "message" or "msg" => GraphEdgeCategory.Message,
            _ => GraphEdgeCategory.Semantic
        };
    }

    public static GraphEdgeCategory ToGraphEdgeCategory(this BoundedContextInteractionType val) => val switch
    {
        BoundedContextInteractionType.Call => GraphEdgeCategory.Call,
        BoundedContextInteractionType.Event => GraphEdgeCategory.Event,
        BoundedContextInteractionType.Database => GraphEdgeCategory.Database,
        BoundedContextInteractionType.Shared => GraphEdgeCategory.Shared,
        BoundedContextInteractionType.Http => GraphEdgeCategory.Http,
        BoundedContextInteractionType.Grpc => GraphEdgeCategory.Grpc,
        BoundedContextInteractionType.Message => GraphEdgeCategory.Message,
        BoundedContextInteractionType.Messaging => GraphEdgeCategory.Messaging,
        BoundedContextInteractionType.ServiceCall => GraphEdgeCategory.ServiceCall,
        _ => GraphEdgeCategory.Call
    };

    public static string ToCanonicalString(this EvidenceKind val) => val switch
    {
        EvidenceKind.Endpoint => "endpoint",
        EvidenceKind.EntryPoint => "entrypoint",
        EvidenceKind.HttpCall => "http_call",
        EvidenceKind.GrpcCall => "grpc_call",
        EvidenceKind.WcfCall => "wcf_call",
        EvidenceKind.Publish => "publish",
        EvidenceKind.Subscribe => "subscribe",
        EvidenceKind.Query => "query",
        EvidenceKind.Procedure => "procedure",
        EvidenceKind.Orm => "orm",
        EvidenceKind.Table => "table",
        EvidenceKind.Config => "config",
        EvidenceKind.Migration => "migration",
        EvidenceKind.LibraryCall => "library_call",
        EvidenceKind.TestCall => "test_call",
        EvidenceKind.TypeReference => "type_reference",
        _ => ""
    };

    public static EvidenceKind ParseEvidenceKind(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return EvidenceKind.Unspecified;
        return raw.Trim().ToLowerInvariant() switch
        {
            "endpoint" => EvidenceKind.Endpoint,
            "entrypoint" or "entry_point" => EvidenceKind.EntryPoint,
            "http_call" or "httpcall" => EvidenceKind.HttpCall,
            "grpc_call" or "grpccall" => EvidenceKind.GrpcCall,
            "wcf_call" or "wcfcall" => EvidenceKind.WcfCall,
            "publish" => EvidenceKind.Publish,
            "subscribe" => EvidenceKind.Subscribe,
            "query" => EvidenceKind.Query,
            "procedure" => EvidenceKind.Procedure,
            "orm" => EvidenceKind.Orm,
            "table" => EvidenceKind.Table,
            "config" or "configuration" => EvidenceKind.Config,
            "migration" => EvidenceKind.Migration,
            "library_call" or "librarycall" => EvidenceKind.LibraryCall,
            "test_call" or "testcall" => EvidenceKind.TestCall,
            "type_reference" or "typereference" => EvidenceKind.TypeReference,
            _ => EvidenceKind.Unspecified
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string FormatEnumValue<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        if (typeof(TEnum) == typeof(NodeKind))
            return Unsafe.As<TEnum, NodeKind>(ref value).ToCypherLabel();
        if (typeof(TEnum) == typeof(RelationshipKind))
            return Unsafe.As<TEnum, RelationshipKind>(ref value).ToCypherType();
        if (typeof(TEnum) == typeof(DatabaseType))
            return Unsafe.As<TEnum, DatabaseType>(ref value).ToCanonicalString();
        if (typeof(TEnum) == typeof(MemberKind))
            return Unsafe.As<TEnum, MemberKind>(ref value).ToCanonicalString();
        if (typeof(TEnum) == typeof(TypeKind))
            return Unsafe.As<TEnum, TypeKind>(ref value).ToCanonicalString();
        if (typeof(TEnum) == typeof(BrokerType))
            return Unsafe.As<TEnum, BrokerType>(ref value).ToCanonicalString();
        if (typeof(TEnum) == typeof(EntryPointType))
            return Unsafe.As<TEnum, EntryPointType>(ref value).ToCanonicalString();
        if (typeof(TEnum) == typeof(DomainType))
            return Unsafe.As<TEnum, DomainType>(ref value).ToCanonicalString();
        if (typeof(TEnum) == typeof(CloudResourceType))
            return Unsafe.As<TEnum, CloudResourceType>(ref value).ToCanonicalString();
        if (typeof(TEnum) == typeof(OperationType))
            return Unsafe.As<TEnum, OperationType>(ref value).ToCanonicalString();
        if (typeof(TEnum) == typeof(ScanIssueCategory))
            return Unsafe.As<TEnum, ScanIssueCategory>(ref value).ToCanonicalString();
        if (typeof(TEnum) == typeof(ProjectLanguage))
            return Unsafe.As<TEnum, ProjectLanguage>(ref value).ToCanonicalString();
        if (typeof(TEnum) == typeof(ProjectArchetype))
            return Unsafe.As<TEnum, ProjectArchetype>(ref value).ToCanonicalString();
        if (typeof(TEnum) == typeof(SyntacticSymbolKind))
            return Unsafe.As<TEnum, SyntacticSymbolKind>(ref value).ToCanonicalString();
        if (typeof(TEnum) == typeof(AppType))
            return Unsafe.As<TEnum, AppType>(ref value).ToCanonicalString();
        if (typeof(TEnum) == typeof(ExternalServiceCategory))
            return Unsafe.As<TEnum, ExternalServiceCategory>(ref value).ToCanonicalString();
        if (typeof(TEnum) == typeof(ProjectMapModuleType))
            return Unsafe.As<TEnum, ProjectMapModuleType>(ref value).ToCanonicalString();
        if (typeof(TEnum) == typeof(BuiltInQueryCategory))
            return Unsafe.As<TEnum, BuiltInQueryCategory>(ref value).ToCanonicalString();
        if (typeof(TEnum) == typeof(QueryParameterType))
            return Unsafe.As<TEnum, QueryParameterType>(ref value).ToCanonicalString();
        if (typeof(TEnum) == typeof(BoundedContextInteractionType))
            return Unsafe.As<TEnum, BoundedContextInteractionType>(ref value).ToCanonicalString();
        if (typeof(TEnum) == typeof(GraphEdgeCategory))
            return Unsafe.As<TEnum, GraphEdgeCategory>(ref value).ToCanonicalString();
        if (typeof(TEnum) == typeof(EvidenceKind))
            return Unsafe.As<TEnum, EvidenceKind>(ref value).ToCanonicalString();
        return value.ToString();
    }

    public static string FormatEnumBoxed(Enum value)
    {
        return value switch
        {
            NodeKind nk => nk.ToCypherLabel(),
            RelationshipKind rk => rk.ToCypherType(),
            DatabaseType dt => dt.ToCanonicalString(),
            MemberKind mk => mk.ToCanonicalString(),
            TypeKind tk => tk.ToCanonicalString(),
            BrokerType bt => bt.ToCanonicalString(),
            EntryPointType ep => ep.ToCanonicalString(),
            DomainType dm => dm.ToCanonicalString(),
            CloudResourceType cr => cr.ToCanonicalString(),
            OperationType op => op.ToCanonicalString(),
            ScanIssueCategory sc => sc.ToCanonicalString(),
            ProjectLanguage pl => pl.ToCanonicalString(),
            ProjectArchetype pa => pa.ToCanonicalString(),
            SyntacticSymbolKind ss => ss.ToCanonicalString(),
            AppType at => at.ToCanonicalString(),
            ExternalServiceCategory es => es.ToCanonicalString(),
            ProjectMapModuleType pm => pm.ToCanonicalString(),
            BuiltInQueryCategory bq => bq.ToCanonicalString(),
            QueryParameterType qp => qp.ToCanonicalString(),
            BoundedContextInteractionType bc => bc.ToCanonicalString(),
            GraphEdgeCategory ge => ge.ToCanonicalString(),
            EvidenceKind ek => ek.ToCanonicalString(),
            _ => value.ToString()
        };
    }

    public static string FormatEnumObject(Enum e) => e switch
    {
        NodeKind nk => nk.ToCypherLabel(),
        RelationshipKind rk => rk.ToCypherType(),
        DatabaseType dt => dt.ToCanonicalString(),
        MemberKind mk => mk.ToCanonicalString(),
        TypeKind tk => tk.ToCanonicalString(),
        BrokerType bt => bt.ToCanonicalString(),
        EntryPointType ept => ept.ToCanonicalString(),
        DomainType dom => dom.ToCanonicalString(),
        CloudResourceType crt => crt.ToCanonicalString(),
        OperationType ot => ot.ToCanonicalString(),
        ScanIssueCategory sic => sic.ToCanonicalString(),
        ProjectLanguage pl => pl.ToCanonicalString(),
        ProjectArchetype pa => pa.ToCanonicalString(),
        SyntacticSymbolKind ssk => ssk.ToCanonicalString(),
        AppType at => at.ToCanonicalString(),
        ExternalServiceCategory esc => esc.ToCanonicalString(),
        ProjectMapModuleType pmt => pmt.ToCanonicalString(),
        BuiltInQueryCategory bqc => bqc.ToCanonicalString(),
        QueryParameterType qpt => qpt.ToCanonicalString(),
        BoundedContextInteractionType bcit => bcit.ToCanonicalString(),
        GraphEdgeCategory gec => gec.ToCanonicalString(),
        EvidenceKind ek => ek.ToCanonicalString(),
        _ => e.ToString()
    };
}

public sealed class RelationshipKindJsonConverter : JsonConverter<RelationshipKind>
{
    public override RelationshipKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String ? EnumExtensions.ParseRelationshipKind(reader.GetString()) : RelationshipKind.Unspecified;
    public override void Write(Utf8JsonWriter writer, RelationshipKind value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToCypherType());
}

public sealed class DatabaseTypeJsonConverter : JsonConverter<DatabaseType>
{
    public override DatabaseType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String ? EnumExtensions.ParseDatabaseType(reader.GetString()) : DatabaseType.Relational;
    public override void Write(Utf8JsonWriter writer, DatabaseType value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToCanonicalString());
}

public sealed class MemberKindJsonConverter : JsonConverter<MemberKind>
{
    public override MemberKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String ? TaxonomyEnumExtensions.ParseMemberKind(reader.GetString()) : MemberKind.Field;
    public override void Write(Utf8JsonWriter writer, MemberKind value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToCanonicalString());
}

public sealed class TypeKindJsonConverter : JsonConverter<TypeKind>
{
    public override TypeKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String ? TaxonomyEnumExtensions.ParseTypeKind(reader.GetString()) : TypeKind.Class;
    public override void Write(Utf8JsonWriter writer, TypeKind value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToCanonicalString());
}

public sealed class BrokerTypeJsonConverter : JsonConverter<BrokerType>
{
    public override BrokerType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String ? TaxonomyEnumExtensions.ParseBrokerType(reader.GetString()) : BrokerType.Generic;
    public override void Write(Utf8JsonWriter writer, BrokerType value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToCanonicalString());
}

public sealed class EntryPointTypeJsonConverter : JsonConverter<EntryPointType>
{
    public override EntryPointType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String ? TaxonomyEnumExtensions.ParseEntryPointType(reader.GetString()) : EntryPointType.Main;
    public override void Write(Utf8JsonWriter writer, EntryPointType value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToCanonicalString());
}

public sealed class DomainTypeJsonConverter : JsonConverter<DomainType>
{
    public override DomainType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String ? TaxonomyEnumExtensions.ParseDomainType(reader.GetString()) : DomainType.Core;
    public override void Write(Utf8JsonWriter writer, DomainType value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToCanonicalString());
}

public sealed class CloudResourceTypeJsonConverter : JsonConverter<CloudResourceType>
{
    public override CloudResourceType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String ? TaxonomyEnumExtensions.ParseCloudResourceType(reader.GetString()) : CloudResourceType.CloudService;
    public override void Write(Utf8JsonWriter writer, CloudResourceType value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToCanonicalString());
}

public sealed class OperationTypeJsonConverter : JsonConverter<OperationType>
{
    public override OperationType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String ? TaxonomyEnumExtendedExtensions.ParseOperationType(reader.GetString()) : OperationType.Unspecified;
    public override void Write(Utf8JsonWriter writer, OperationType value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToCanonicalString());
}

public sealed class ScanIssueCategoryJsonConverter : JsonConverter<ScanIssueCategory>
{
    public override ScanIssueCategory Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String ? TaxonomyEnumExtendedExtensions.ParseScanIssueCategory(reader.GetString()) : ScanIssueCategory.SyntaxError;
    public override void Write(Utf8JsonWriter writer, ScanIssueCategory value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToCanonicalString());
}

public sealed class ProjectLanguageJsonConverter : JsonConverter<ProjectLanguage>
{
    public override ProjectLanguage Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String ? TaxonomyEnumExtendedExtensions.ParseProjectLanguage(reader.GetString()) : ProjectLanguage.Unknown;
    public override void Write(Utf8JsonWriter writer, ProjectLanguage value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToCanonicalString());
}

public sealed class ProjectArchetypeJsonConverter : JsonConverter<ProjectArchetype>
{
    public override ProjectArchetype Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String ? TaxonomyEnumExtendedExtensions.ParseProjectArchetype(reader.GetString()) : ProjectArchetype.Unclassified;
    public override void Write(Utf8JsonWriter writer, ProjectArchetype value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToCanonicalString());
}

public sealed class SyntacticSymbolKindJsonConverter : JsonConverter<SyntacticSymbolKind>
{
    public override SyntacticSymbolKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String ? TaxonomyEnumExtendedExtensions.ParseSyntacticSymbolKind(reader.GetString()) : SyntacticSymbolKind.Unknown;
    public override void Write(Utf8JsonWriter writer, SyntacticSymbolKind value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToCanonicalString());
}

public sealed class AppTypeJsonConverter : JsonConverter<AppType>
{
    public override AppType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String ? TaxonomyViewEnumExtensions.ParseAppType(reader.GetString()) : AppType.Unspecified;
    public override void Write(Utf8JsonWriter writer, AppType value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToCanonicalString());
}

public sealed class ExternalServiceCategoryJsonConverter : JsonConverter<ExternalServiceCategory>
{
    public override ExternalServiceCategory Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String ? TaxonomyViewEnumExtensions.ParseExternalServiceCategory(reader.GetString()) : ExternalServiceCategory.Unspecified;
    public override void Write(Utf8JsonWriter writer, ExternalServiceCategory value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToCanonicalString());
}

public sealed class ProjectMapModuleTypeJsonConverter : JsonConverter<ProjectMapModuleType>
{
    public override ProjectMapModuleType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String ? TaxonomyViewEnumExtensions.ParseProjectMapModuleType(reader.GetString()) : ProjectMapModuleType.Module;
    public override void Write(Utf8JsonWriter writer, ProjectMapModuleType value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToCanonicalString());
}

public sealed class BuiltInQueryCategoryJsonConverter : JsonConverter<BuiltInQueryCategory>
{
    public override BuiltInQueryCategory Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String ? TaxonomyViewEnumExtensions.ParseBuiltInQueryCategory(reader.GetString()) : BuiltInQueryCategory.General;
    public override void Write(Utf8JsonWriter writer, BuiltInQueryCategory value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToCanonicalString());
}

public sealed class QueryParameterTypeJsonConverter : JsonConverter<QueryParameterType>
{
    public override QueryParameterType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String ? TaxonomyViewEnumExtensions.ParseQueryParameterType(reader.GetString()) : QueryParameterType.String;
    public override void Write(Utf8JsonWriter writer, QueryParameterType value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToCanonicalString());
}

public sealed class BoundedContextInteractionTypeJsonConverter : JsonConverter<BoundedContextInteractionType>
{
    public override BoundedContextInteractionType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String ? TaxonomyViewEnumExtensions.ParseBoundedContextInteractionType(reader.GetString()) : BoundedContextInteractionType.Call;
    public override void Write(Utf8JsonWriter writer, BoundedContextInteractionType value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToCanonicalString());
}

public sealed class GraphEdgeCategoryJsonConverter : JsonConverter<GraphEdgeCategory>
{
    public override GraphEdgeCategory Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String ? TaxonomyViewEnumExtensions.ParseGraphEdgeCategory(reader.GetString()) : GraphEdgeCategory.Unspecified;
    public override void Write(Utf8JsonWriter writer, GraphEdgeCategory value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToCanonicalString());
}

public sealed class EvidenceKindJsonConverter : JsonConverter<EvidenceKind>
{
    public override EvidenceKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String ? TaxonomyViewEnumExtensions.ParseEvidenceKind(reader.GetString()) : EvidenceKind.Unspecified;
    public override void Write(Utf8JsonWriter writer, EvidenceKind value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToCanonicalString());
}
