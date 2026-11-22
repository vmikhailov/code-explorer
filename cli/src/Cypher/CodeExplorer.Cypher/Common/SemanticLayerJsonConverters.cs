using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeExplorer.Cypher.Common;

public static class SemanticLayerEnumExtensions
{
    public static string ToCanonicalString(this CacheEngine val) => val switch
    {
        CacheEngine.Memory => "memory",
        CacheEngine.Distributed => "distributed",
        CacheEngine.Redis => "redis",
        CacheEngine.Memcached => "memcached",
        CacheEngine.Hybrid => "hybrid",
        CacheEngine.HttpOutput => "http_output",
        _ => "cache"
    };

    public static CacheEngine ParseCacheEngine(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return CacheEngine.Unspecified;
        return raw.Trim().ToLowerInvariant() switch
        {
            "memory" or "inmemory" or "local" => CacheEngine.Memory,
            "distributed" => CacheEngine.Distributed,
            "redis" => CacheEngine.Redis,
            "memcached" => CacheEngine.Memcached,
            "hybrid" => CacheEngine.Hybrid,
            "http_output" or "http" or "output" => CacheEngine.HttpOutput,
            _ => CacheEngine.Unspecified
        };
    }

    public static string ToCanonicalString(this TransactionKind val) => val switch
    {
        TransactionKind.Explicit => "explicit",
        TransactionKind.Declarative => "declarative",
        TransactionKind.PipelineBehavior => "pipeline_behavior",
        TransactionKind.Ambient => "ambient",
        _ => "explicit"
    };

    public static TransactionKind ParseTransactionKind(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return TransactionKind.Unspecified;
        return raw.Trim().ToLowerInvariant() switch
        {
            "explicit" => TransactionKind.Explicit,
            "declarative" or "annotation" => TransactionKind.Declarative,
            "pipeline_behavior" or "behavior" => TransactionKind.PipelineBehavior,
            "ambient" or "scope" => TransactionKind.Ambient,
            _ => TransactionKind.Explicit
        };
    }

    public static string ToCanonicalString(this PolicyKind val) => val switch
    {
        PolicyKind.Authenticated => "authenticated",
        PolicyKind.Role => "role",
        PolicyKind.Permission => "permission",
        PolicyKind.CustomGuard => "custom_guard",
        PolicyKind.Anonymous => "anonymous",
        _ => "authenticated"
    };

    public static PolicyKind ParsePolicyKind(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return PolicyKind.Unspecified;
        return raw.Trim().ToLowerInvariant() switch
        {
            "authenticated" or "auth" => PolicyKind.Authenticated,
            "role" or "roles" => PolicyKind.Role,
            "permission" or "permissions" or "policy" => PolicyKind.Permission,
            "custom_guard" or "guard" => PolicyKind.CustomGuard,
            "anonymous" or "public" => PolicyKind.Anonymous,
            _ => PolicyKind.Authenticated
        };
    }

    public static string ToCanonicalString(this JobKind val) => val switch
    {
        JobKind.Cron => "cron",
        JobKind.Recurring => "recurring",
        JobKind.FireAndForget => "fire_and_forget",
        JobKind.Workflow => "workflow",
        JobKind.Activity => "activity",
        JobKind.BackgroundWorker => "background_worker",
        _ => "recurring"
    };

    public static JobKind ParseJobKind(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return JobKind.Unspecified;
        return raw.Trim().ToLowerInvariant() switch
        {
            "cron" => JobKind.Cron,
            "recurring" or "interval" => JobKind.Recurring,
            "fire_and_forget" or "enqueue" => JobKind.FireAndForget,
            "workflow" => JobKind.Workflow,
            "activity" => JobKind.Activity,
            "background_worker" or "worker" => JobKind.BackgroundWorker,
            _ => JobKind.Recurring
        };
    }
}

public sealed class CacheEngineJsonConverter : JsonConverter<CacheEngine>
{
    public override CacheEngine Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String ? SemanticLayerEnumExtensions.ParseCacheEngine(reader.GetString()) : CacheEngine.Unspecified;
    public override void Write(Utf8JsonWriter writer, CacheEngine value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToCanonicalString());
}

public sealed class TransactionKindJsonConverter : JsonConverter<TransactionKind>
{
    public override TransactionKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String ? SemanticLayerEnumExtensions.ParseTransactionKind(reader.GetString()) : TransactionKind.Unspecified;
    public override void Write(Utf8JsonWriter writer, TransactionKind value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToCanonicalString());
}

public sealed class PolicyKindJsonConverter : JsonConverter<PolicyKind>
{
    public override PolicyKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String ? SemanticLayerEnumExtensions.ParsePolicyKind(reader.GetString()) : PolicyKind.Unspecified;
    public override void Write(Utf8JsonWriter writer, PolicyKind value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToCanonicalString());
}

public sealed class JobKindJsonConverter : JsonConverter<JobKind>
{
    public override JobKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String ? SemanticLayerEnumExtensions.ParseJobKind(reader.GetString()) : JobKind.Unspecified;
    public override void Write(Utf8JsonWriter writer, JobKind value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToCanonicalString());
}
