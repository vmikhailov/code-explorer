namespace CodeExplorer.Parser.Terraform;

/// <summary>
/// resource_type → ce Layer 4 semantic-kind lookup. Data-driven and extensible — add rows as
/// coverage grows.
///
/// For Topic/Database kinds it also returns the broker_type / db_type so the semantic node's
/// canonical ID matches the one application parsers build:
///   Topic:    {workspaceId}:top:{brokerType}:{name}
///   Database: {workspaceId}:db:{dbType}:{name}
/// Reconciliation is by shared node ID, so the broker/db type must match what application parsers
/// use (e.g. aws_msk_cluster -> "kafka", aws_sqs_queue -> "sqs", aws_dynamodb_table -> "dynamodb").
/// </summary>
public static class CloudResourceCatalog
{
    public enum SemanticKind { Database, Topic, CloudService, None }

    public readonly record struct Classification(SemanticKind Kind, string? SubType);

    // Exact resource_type → (kind, broker_type|db_type). SubType is null for CloudService.
    private static readonly Dictionary<string, Classification> Exact = new(StringComparer.OrdinalIgnoreCase)
    {
        // Databases (SubType = db_type)
        ["aws_db_instance"] = new(SemanticKind.Database, "relational"),
        ["aws_rds_cluster"] = new(SemanticKind.Database, "relational"),
        ["aws_rds_cluster_instance"] = new(SemanticKind.Database, "relational"),
        ["azurerm_mssql_server"] = new(SemanticKind.Database, "sqlserver"),
        ["azurerm_postgresql_server"] = new(SemanticKind.Database, "postgres"),
        ["google_sql_database_instance"] = new(SemanticKind.Database, "relational"),
        ["aws_dynamodb_table"] = new(SemanticKind.Database, "dynamodb"),
        ["aws_elasticache_cluster"] = new(SemanticKind.Database, "redis"),  // engine-blind: a memcached cluster is still typed redis (KNOWN_ISSUES F4)
        ["aws_memorydb_cluster"] = new(SemanticKind.Database, "redis"),
        ["aws_docdb_cluster"] = new(SemanticKind.Database, "mongodb"),

        // Topics / queues / streams (SubType = broker_type)
        ["aws_msk_cluster"] = new(SemanticKind.Topic, "kafka"),
        ["aws_msk_serverless_cluster"] = new(SemanticKind.Topic, "kafka"),
        ["aws_sqs_queue"] = new(SemanticKind.Topic, "sqs"),
        ["aws_sns_topic"] = new(SemanticKind.Topic, "sns"),
        ["aws_kinesis_stream"] = new(SemanticKind.Topic, "kinesis"),
        ["google_pubsub_topic"] = new(SemanticKind.Topic, "pubsub"),
        ["kafka_topic"] = new(SemanticKind.Topic, "kafka"),
    };

    public static Classification Classify(string resourceType)
    {
        if (Exact.TryGetValue(resourceType, out var cls)) return cls;

        // Fallback: recognised provider-prefixed resources are generic cloud services.
        if (resourceType.StartsWith("aws_", StringComparison.OrdinalIgnoreCase)
            || resourceType.StartsWith("azurerm_", StringComparison.OrdinalIgnoreCase)
            || resourceType.StartsWith("google_", StringComparison.OrdinalIgnoreCase)
            || resourceType.StartsWith("azuread_", StringComparison.OrdinalIgnoreCase)
            || resourceType.StartsWith("awscc_", StringComparison.OrdinalIgnoreCase))
        {
            return new Classification(SemanticKind.CloudService, null);
        }

        return new Classification(SemanticKind.None, null);
    }
}
