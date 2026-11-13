using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Parser.Terraform;

/// <summary>
/// resource_type → ce Layer 4 semantic-kind lookup. Data-driven and extensible — add rows as
/// coverage grows.
///
/// For Topic/Database kinds it also returns the broker / database type so the semantic node's
/// canonical ID matches the one application parsers build:
///   Topic:    {workspaceId}:top:{brokerType}:{name}
///   Database: {workspaceId}:db:{dbType}:{name}
/// Reconciliation is by shared node ID, so the broker/db type must match what application parsers
/// use (e.g. aws_msk_cluster → Kafka, aws_sqs_queue → Sqs, aws_dynamodb_table → KeyValue).
/// Other recognised provider resources become cloud services, typed by <see cref="CloudResourceType"/>
/// where a matching value exists.
/// </summary>
public static class CloudResourceCatalog
{
    public enum SemanticKind { Database, Topic, CloudService, None }

    public readonly record struct Classification(
        SemanticKind Kind,
        DatabaseType DbType = DatabaseType.Unknown,
        BrokerType BrokerType = BrokerType.Unspecified,
        CloudResourceType CloudType = CloudResourceType.Unspecified);

    private static Classification Db(DatabaseType type) => new(SemanticKind.Database, DbType: type);
    private static Classification Topic(BrokerType type) => new(SemanticKind.Topic, BrokerType: type);
    private static Classification Cloud(CloudResourceType type) => new(SemanticKind.CloudService, CloudType: type);

    // Exact resource_type → database / topic classification.
    private static readonly Dictionary<string, Classification> Exact = new(StringComparer.OrdinalIgnoreCase)
    {
        // Databases
        ["aws_db_instance"] = Db(DatabaseType.Relational),
        ["aws_rds_cluster"] = Db(DatabaseType.Relational),
        ["aws_rds_cluster_instance"] = Db(DatabaseType.Relational),
        ["azurerm_mssql_server"] = Db(DatabaseType.Relational),
        ["azurerm_postgresql_server"] = Db(DatabaseType.Relational),
        ["google_sql_database_instance"] = Db(DatabaseType.Relational),
        ["aws_dynamodb_table"] = Db(DatabaseType.KeyValue),
        ["aws_elasticache_cluster"] = Db(DatabaseType.Cache),  // engine-blind: a memcached cluster is still typed cache
        ["aws_memorydb_cluster"] = Db(DatabaseType.Cache),
        ["aws_docdb_cluster"] = Db(DatabaseType.Document),

        // Topics / queues / streams
        ["aws_msk_cluster"] = Topic(BrokerType.Kafka),
        ["aws_msk_serverless_cluster"] = Topic(BrokerType.Kafka),
        ["aws_sqs_queue"] = Topic(BrokerType.Sqs),
        ["aws_sns_topic"] = Topic(BrokerType.Sns),
        ["aws_kinesis_stream"] = Topic(BrokerType.Kinesis),
        ["google_pubsub_topic"] = Topic(BrokerType.PubSub),
        ["kafka_topic"] = Topic(BrokerType.Kafka),
    };

    // Resource-type prefix → cloud service type. First match wins, so more specific prefixes go first.
    private static readonly (string Prefix, CloudResourceType Type)[] CloudServicePrefixes =
    [
        ("aws_s3_", CloudResourceType.S3Bucket),
        ("aws_lambda_", CloudResourceType.Lambda),
        ("aws_iam_", CloudResourceType.Iam),
        ("aws_kms_", CloudResourceType.Kms),
        ("aws_secretsmanager_", CloudResourceType.SecretsManager),
        ("aws_ssm_parameter", CloudResourceType.ParameterStore),
        ("aws_api_gateway_", CloudResourceType.ApiGateway),
        ("aws_apigatewayv2_", CloudResourceType.ApiGateway),
        ("aws_cloudfront_", CloudResourceType.CloudFront),
        ("aws_sfn_", CloudResourceType.StepFunctions),
        ("aws_cloudwatch_event_", CloudResourceType.EventBridge),
        ("aws_cognito_", CloudResourceType.Cognito),
        ("aws_ses", CloudResourceType.Ses),
        ("azurerm_storage_", CloudResourceType.BlobStorage),
        ("azurerm_key_vault", CloudResourceType.KeyVault),
        ("azurerm_cosmosdb_", CloudResourceType.CosmosDb),
        ("azurerm_function_app", CloudResourceType.AzureFunction),
        ("azurerm_linux_function_app", CloudResourceType.AzureFunction),
        ("azurerm_windows_function_app", CloudResourceType.AzureFunction),
        ("google_storage_bucket", CloudResourceType.GcsBucket),
        ("google_firestore_", CloudResourceType.Firestore),
        ("google_cloudfunctions", CloudResourceType.CloudFunction),
        ("google_secret_manager_", CloudResourceType.SecretManager),
        ("google_kms_", CloudResourceType.Kms),
    ];

    // Provider prefixes whose remaining resources are generic cloud services.
    private static readonly string[] CloudProviderPrefixes = ["aws_", "azurerm_", "google_", "azuread_", "awscc_"];

    // Cloud provider of a resource type, from its provider prefix; null for non-cloud providers.
    public static string? ProviderOf(TerraformResourceType resourceType)
    {
        var name = resourceType.Name;
        if (name.StartsWith("aws_", StringComparison.OrdinalIgnoreCase) || name.StartsWith("awscc_", StringComparison.OrdinalIgnoreCase)) return "aws";
        if (name.StartsWith("azurerm_", StringComparison.OrdinalIgnoreCase) || name.StartsWith("azuread_", StringComparison.OrdinalIgnoreCase)) return "azure";
        if (name.StartsWith("google_", StringComparison.OrdinalIgnoreCase)) return "gcp";
        return null;
    }

    // Attributes holding the name application code addresses the resource by, in priority order:
    // the database name of a relational instance (what connection strings select), otherwise the
    // resource's own name (DynamoDB table, queue, topic, bucket, function).
    private static readonly string[] RelationalNameAttributes = ["db_name", "database_name", "identifier", "cluster_identifier"];
    private static readonly string[] ResourceNameAttributes = ["name", "cluster_id", "cluster_name"];
    private static readonly string[] CloudServiceNameAttributes = ["name", "bucket", "function_name"];

    public static IReadOnlyList<string> NameAttributesFor(Classification classification) => classification switch
    {
        { Kind: SemanticKind.Database, DbType: DatabaseType.Relational } => RelationalNameAttributes,
        { Kind: SemanticKind.Database or SemanticKind.Topic } => ResourceNameAttributes,
        { Kind: SemanticKind.CloudService } => CloudServiceNameAttributes,
        _ => []
    };

    // Database engine by resource type, in the spelling Layer 5 database consolidation recognises.
    private static readonly Dictionary<string, string> FixedEngines = new(StringComparer.OrdinalIgnoreCase)
    {
        ["aws_dynamodb_table"] = "dynamodb",
        ["aws_memorydb_cluster"] = "redis",
        ["aws_docdb_cluster"] = "mongodb",
        ["azurerm_mssql_server"] = "sqlserver",
        ["azurerm_postgresql_server"] = "postgresql",
    };

    // Resource types whose engine is configured by an attribute (e.g. engine = "postgres").
    private static readonly Dictionary<string, string> EngineAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["aws_db_instance"] = "engine",
        ["aws_rds_cluster"] = "engine",
        ["aws_rds_cluster_instance"] = "engine",
        ["aws_elasticache_cluster"] = "engine",
        ["google_sql_database_instance"] = "database_version",
    };

    // Engine attribute values that differ from the consolidation spelling, matched by prefix
    // (aurora-postgresql, sqlserver-ex, oracle-ee, POSTGRES_15, MYSQL_8_0, ...).
    private static readonly (string Prefix, string Engine)[] EnginePrefixes =
    [
        ("aurora-postgresql", "postgresql"),
        ("aurora-mysql", "mysql"),
        ("aurora", "mysql"),
        ("postgres", "postgresql"),
        ("mysql", "mysql"),
        ("mariadb", "mariadb"),
        ("sqlserver", "sqlserver"),
        ("oracle", "oracle"),
        ("redis", "redis"),
        ("valkey", "redis"),
        ("memcached", "memcached"),
    ];

    /// <summary>The attribute that configures the engine of <paramref name="resourceType"/>, if any.</summary>
    public static string? EngineAttributeFor(TerraformResourceType resourceType) =>
        EngineAttributes.GetValueOrDefault(resourceType.Name);

    /// <summary>
    /// The database engine of <paramref name="resourceType"/>: fixed by type, or normalised from the
    /// literal value of its engine attribute. Null when unknown.
    /// </summary>
    public static string? EngineFor(TerraformResourceType resourceType, string? engineAttributeValue)
    {
        if (FixedEngines.TryGetValue(resourceType.Name, out var fixedEngine)) return fixedEngine;
        if (string.IsNullOrWhiteSpace(engineAttributeValue)) return null;

        foreach (var (prefix, engine) in EnginePrefixes)
        {
            if (engineAttributeValue.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return engine;
        }
        return engineAttributeValue.ToLowerInvariant();
    }

    public static Classification Classify(TerraformResourceType resourceType)
    {
        var name = resourceType.Name;
        if (Exact.TryGetValue(name, out var cls)) return cls;

        foreach (var (prefix, type) in CloudServicePrefixes)
        {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return Cloud(type);
        }

        foreach (var prefix in CloudProviderPrefixes)
        {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return Cloud(CloudResourceType.CloudService);
        }

        return new Classification(SemanticKind.None);
    }
}
