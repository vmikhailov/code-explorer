namespace CodeExplorer.Parser.Terraform;

public enum InfraCategory : byte
{
    CloudResource,
    Database,
    Topic
}

public sealed record InfraClassification(
    InfraCategory Category,
    string Provider,
    string ResourceRole,
    string? DbSystem = null,
    string? BrokerEngine = null);

/// <summary>
/// Maps Terraform/OpenTofu resource type identifiers (e.g. <c>aws_db_instance</c>, <c>aws_sqs_queue</c>,
/// <c>aws_s3_bucket</c>, <c>aws_vpc</c>) into canonical ontology classifications.
/// </summary>
public static class CloudResourceCatalog
{
    private const string ProviderAws = "aws";
    private const string ProviderGcp = "gcp";
    private const string ProviderAzure = "azure";
    private const string ProviderKubernetes = "kubernetes";
    private const string ProviderDigitalOcean = "digitalocean";
    private const string ProviderCloudflare = "cloudflare";
    private const string ProviderCloud = "cloud";

    private const string RoleDatabase = "database";
    private const string RoleMessaging = "messaging";
    private const string RoleStorage = "storage";
    private const string RoleCompute = "compute";
    private const string RoleNetwork = "network";
    private const string RoleSecurity = "security";
    private const string RoleIdentity = "identity";
    private const string RoleSecrets = "secrets";
    private const string RoleCdn = "cdn";
    private const string RoleDns = "dns";
    private const string RoleContainer = "container";
    private const string RoleMonitoring = "monitoring";
    private const string RoleInfrastructure = "infrastructure";

    private const string DbRelational = "relational";
    private const string DbKeyValue = "keyvalue";
    private const string DbDocument = "document";
    private const string DbCache = "cache";
    private const string DbSearch = "search";
    private const string DbAnalytics = "analytics";
    private const string DbGraph = "graph";

    private const string BrokerSqs = "sqs";
    private const string BrokerSns = "sns";
    private const string BrokerKafka = "kafka";
    private const string BrokerRabbitMq = "rabbitmq";
    private const string BrokerPubSub = "pubsub";
    private const string BrokerServiceBus = "servicebus";
    private const string BrokerEventGrid = "eventgrid";

    private static readonly Dictionary<string, (string DbKind, string Provider)> ExactDatabases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["aws_db_instance"] = (DbRelational, ProviderAws),
        ["aws_rds_cluster"] = (DbRelational, ProviderAws),
        ["aws_rds_cluster_instance"] = (DbRelational, ProviderAws),
        ["aws_dynamodb_table"] = (DbKeyValue, ProviderAws),
        ["aws_elasticache_cluster"] = (DbCache, ProviderAws),
        ["aws_elasticache_replication_group"] = (DbCache, ProviderAws),
        ["aws_memorydb_cluster"] = (DbCache, ProviderAws),
        ["aws_docdb_cluster"] = (DbDocument, ProviderAws),
        ["aws_neptune_cluster"] = (DbGraph, ProviderAws),
        ["aws_opensearch_domain"] = (DbSearch, ProviderAws),
        ["aws_elasticsearch_domain"] = (DbSearch, ProviderAws),
        ["aws_redshift_cluster"] = (DbAnalytics, ProviderAws),

        ["google_sql_database_instance"] = (DbRelational, ProviderGcp),
        ["google_sql_database"] = (DbRelational, ProviderGcp),
        ["google_spanner_instance"] = (DbRelational, ProviderGcp),
        ["google_spanner_database"] = (DbRelational, ProviderGcp),
        ["google_Bigtable_instance"] = (DbKeyValue, ProviderGcp),
        ["google_firestore_database"] = (DbDocument, ProviderGcp),
        ["google_redis_instance"] = (DbCache, ProviderGcp),
        ["google_Bigquery_dataset"] = (DbAnalytics, ProviderGcp),

        ["azurerm_mssql_server"] = (DbRelational, ProviderAzure),
        ["azurerm_mssql_database"] = (DbRelational, ProviderAzure),
        ["azurerm_postgresql_server"] = (DbRelational, ProviderAzure),
        ["azurerm_postgresql_flexible_server"] = (DbRelational, ProviderAzure),
        ["azurerm_postgresql_flexible_server_database"] = (DbRelational, ProviderAzure),
        ["azurerm_mysql_server"] = (DbRelational, ProviderAzure),
        ["azurerm_mysql_flexible_server"] = (DbRelational, ProviderAzure),
        ["azurerm_cosmosdb_account"] = (DbDocument, ProviderAzure),
        ["azurerm_cosmosdb_sql_database"] = (DbDocument, ProviderAzure),
        ["azurerm_cosmosdb_mongo_database"] = (DbDocument, ProviderAzure),
        ["azurerm_redis_cache"] = (DbCache, ProviderAzure),
        ["azurerm_search_service"] = (DbSearch, ProviderAzure),

        ["digitalocean_database_cluster"] = (DbRelational, ProviderDigitalOcean)
    };

    private static readonly Dictionary<string, (string BrokerKind, string Provider)> ExactTopics = new(StringComparer.OrdinalIgnoreCase)
    {
        ["aws_sqs_queue"] = (BrokerSqs, ProviderAws),
        ["aws_sns_topic"] = (BrokerSns, ProviderAws),
        ["aws_kinesis_stream"] = (BrokerKafka, ProviderAws),
        ["aws_msk_cluster"] = (BrokerKafka, ProviderAws),
        ["aws_msk_serverless_cluster"] = (BrokerKafka, ProviderAws),
        ["aws_mq_broker"] = (BrokerRabbitMq, ProviderAws),
        ["aws_cloudwatch_event_bus"] = (BrokerEventGrid, ProviderAws),

        ["google_pubsub_topic"] = (BrokerPubSub, ProviderGcp),
        ["google_pubsub_subscription"] = (BrokerPubSub, ProviderGcp),

        ["azurerm_servicebus_namespace"] = (BrokerServiceBus, ProviderAzure),
        ["azurerm_servicebus_queue"] = (BrokerServiceBus, ProviderAzure),
        ["azurerm_servicebus_topic"] = (BrokerServiceBus, ProviderAzure),
        ["azurerm_eventhub_namespace"] = (BrokerKafka, ProviderAzure),
        ["azurerm_eventhub"] = (BrokerKafka, ProviderAzure),
        ["azurerm_eventgrid_topic"] = (BrokerEventGrid, ProviderAzure),
        ["azurerm_eventgrid_domain"] = (BrokerEventGrid, ProviderAzure)
    };

    public static InfraClassification Classify(string resourceType)
    {
        if (string.IsNullOrWhiteSpace(resourceType))
        {
            return new InfraClassification(InfraCategory.CloudResource, ProviderCloud, RoleInfrastructure);
        }

        if (ExactDatabases.TryGetValue(resourceType, out var db))
        {
            return new InfraClassification(InfraCategory.Database, db.Provider, RoleDatabase, DbSystem: db.DbKind);
        }

        if (ExactTopics.TryGetValue(resourceType, out var topic))
        {
            return new InfraClassification(InfraCategory.Topic, topic.Provider, RoleMessaging, BrokerEngine: topic.BrokerKind);
        }

        var provider = InferProvider(resourceType);
        var role = InferResourceRole(resourceType);
        return new InfraClassification(InfraCategory.CloudResource, provider, role);
    }

    public static string InferProvider(string resourceTypeOrName)
    {
        if (string.IsNullOrWhiteSpace(resourceTypeOrName)) return ProviderCloud;
        var lower = resourceTypeOrName.Trim().ToLowerInvariant();
        if (lower.StartsWith("aws_", StringComparison.Ordinal) || lower == "aws") return ProviderAws;
        if (lower.StartsWith("google_", StringComparison.Ordinal) || lower is "google" or "google-beta" or "gcp") return ProviderGcp;
        if (lower.StartsWith("azurerm_", StringComparison.Ordinal) || lower.StartsWith("azuread_", StringComparison.Ordinal) || lower is "azurerm" or "azuread" or "azapi" or "azure") return ProviderAzure;
        if (lower.StartsWith("kubernetes_", StringComparison.Ordinal) || lower.StartsWith("helm_", StringComparison.Ordinal) || lower.StartsWith("kubectl_", StringComparison.Ordinal) || lower is "kubernetes" or "helm" or "kubectl") return ProviderKubernetes;
        if (lower.StartsWith("digitalocean_", StringComparison.Ordinal) || lower == "digitalocean") return ProviderDigitalOcean;
        if (lower.StartsWith("cloudflare_", StringComparison.Ordinal) || lower == "cloudflare") return ProviderCloudflare;
        var underscore = lower.IndexOf('_');
        return underscore > 0 ? lower[..underscore] : lower;
    }

    private static string InferResourceRole(string resourceType)
    {
        var lower = resourceType.ToLowerInvariant();

        if (lower.Contains("_s3_", StringComparison.Ordinal) ||
            lower.Contains("storage_bucket", StringComparison.Ordinal) ||
            lower.Contains("storage_account", StringComparison.Ordinal) ||
            lower.Contains("storage_container", StringComparison.Ordinal) ||
            lower.Contains("_efs_", StringComparison.Ordinal))
        {
            return RoleStorage;
        }

        if (lower.Contains("secret", StringComparison.Ordinal) ||
            lower.Contains("key_vault", StringComparison.Ordinal) ||
            lower.Contains("_kms_", StringComparison.Ordinal) ||
            lower.Contains("ssm_parameter", StringComparison.Ordinal))
        {
            return RoleSecrets;
        }

        if (lower.Contains("_iam_", StringComparison.Ordinal) ||
            lower.Contains("cognito", StringComparison.Ordinal) ||
            lower.Contains("identity", StringComparison.Ordinal) ||
            lower.Contains("role_assignment", StringComparison.Ordinal) ||
            lower.Contains("service_account", StringComparison.Ordinal))
        {
            return RoleIdentity;
        }

        if (lower.Contains("waf", StringComparison.Ordinal) ||
            lower.Contains("shield", StringComparison.Ordinal) ||
            lower.Contains("guardduty", StringComparison.Ordinal))
        {
            return RoleSecurity;
        }

        if (lower.Contains("lambda", StringComparison.Ordinal) ||
            lower.Contains("function", StringComparison.Ordinal) ||
            lower.Contains("cloud_run", StringComparison.Ordinal) ||
            lower.Contains("_ecs_", StringComparison.Ordinal) ||
            lower.Contains("_eks_", StringComparison.Ordinal) ||
            lower.Contains("_aks_", StringComparison.Ordinal) ||
            lower.Contains("_gke_", StringComparison.Ordinal) ||
            lower.Contains("container_cluster", StringComparison.Ordinal) ||
            lower.Contains("kubernetes_deployment", StringComparison.Ordinal) ||
            lower.Contains("instance", StringComparison.Ordinal) ||
            lower.Contains("app_service", StringComparison.Ordinal) ||
            lower.Contains("linux_web_app", StringComparison.Ordinal))
        {
            return RoleCompute;
        }

        if (lower.Contains("cloudfront", StringComparison.Ordinal) ||
            lower.Contains("cdn", StringComparison.Ordinal))
        {
            return RoleCdn;
        }

        if (lower.Contains("route53", StringComparison.Ordinal) ||
            lower.Contains("dns", StringComparison.Ordinal))
        {
            return RoleDns;
        }

        if (lower.Contains("vpc", StringComparison.Ordinal) ||
            lower.Contains("subnet", StringComparison.Ordinal) ||
            lower.Contains("security_group", StringComparison.Ordinal) ||
            lower.Contains("network", StringComparison.Ordinal) ||
            lower.Contains("_lb", StringComparison.Ordinal) ||
            lower.Contains("alb", StringComparison.Ordinal) ||
            lower.Contains("api_gateway", StringComparison.Ordinal) ||
            lower.Contains("apigateway", StringComparison.Ordinal) ||
            lower.Contains("ingress", StringComparison.Ordinal) ||
            lower.Contains("firewall", StringComparison.Ordinal))
        {
            return RoleNetwork;
        }

        if (lower.Contains("ecr", StringComparison.Ordinal) ||
            lower.Contains("container_registry", StringComparison.Ordinal) ||
            lower.Contains("artifact_registry", StringComparison.Ordinal))
        {
            return RoleContainer;
        }

        if (lower.Contains("cloudwatch", StringComparison.Ordinal) ||
            lower.Contains("monitor", StringComparison.Ordinal) ||
            lower.Contains("log_", StringComparison.Ordinal))
        {
            return RoleMonitoring;
        }

        return RoleInfrastructure;
    }

    private const string EnginePostgres = "postgres";
    private const string EngineMySql = "mysql";
    private const string EngineMariaDb = "mariadb";
    private const string EngineSqlServer = "sqlserver";
    private const string EngineMsSql = "mssql";
    private const string EngineSqlServerUnderscore = "sql_server";
    private const string EngineDynamoDb = "dynamodb";
    private const string EngineRedis = "redis";
    private const string EngineElastiCache = "elasticache";
    private const string EngineMemoryStore = "memorystore";
    private const string EngineMongoDb = "mongodb";
    private const string EngineMongo = "mongo";
    private const string EngineDocDb = "docdb";
    private const string EngineElasticsearch = "elasticsearch";
    private const string EngineOpenSearch = "opensearch";
    private const string EngineRedshift = "redshift";
    private const string EngineBigQuery = "bigquery";
    private const string EngineSpanner = "spanner";
    private const string EngineFirestore = "firestore";
    private const string EngineBigtable = "bigtable";
    private const string EngineCosmosDb = "cosmosdb";
    private const string EngineNeptune = "neptune";
    private const string EngineOracle = "oracle";

    public static string? InferDatabaseEngine(string resourceType, string? explicitEngine)
    {
        if (!string.IsNullOrWhiteSpace(explicitEngine))
        {
            return NormalizeEngineToken(explicitEngine.Trim().ToLowerInvariant());
        }

        if (string.IsNullOrWhiteSpace(resourceType))
        {
            return null;
        }

        var lower = resourceType.ToLowerInvariant();
        var inferred = InferEngineFromText(lower);
        if (inferred != null)
        {
            return inferred;
        }

        return ExactDatabases.TryGetValue(resourceType, out var db) ? db.DbKind : null;
    }

    private static string NormalizeEngineToken(string lower) =>
        InferEngineFromText(lower) ?? lower;

    private static string? InferEngineFromText(string lower)
    {
        if (lower.Contains(EnginePostgres, StringComparison.Ordinal)) return EnginePostgres;
        if (lower.Contains(EngineMySql, StringComparison.Ordinal) || lower.Contains(EngineMariaDb, StringComparison.Ordinal)) return EngineMySql;
        if (lower.Contains(EngineMsSql, StringComparison.Ordinal) || lower.Contains(EngineSqlServer, StringComparison.Ordinal) || lower.Contains(EngineSqlServerUnderscore, StringComparison.Ordinal)) return EngineSqlServer;
        if (lower.Contains(EngineOracle, StringComparison.Ordinal)) return EngineOracle;
        if (lower.Contains(EngineDynamoDb, StringComparison.Ordinal)) return EngineDynamoDb;
        if (lower.Contains(EngineRedis, StringComparison.Ordinal) || lower.Contains(EngineElastiCache, StringComparison.Ordinal) || lower.Contains(EngineMemoryStore, StringComparison.Ordinal)) return EngineRedis;
        if (lower.Contains(EngineDocDb, StringComparison.Ordinal) || lower.Contains(EngineMongo, StringComparison.Ordinal)) return EngineMongoDb;
        if (lower.Contains(EngineOpenSearch, StringComparison.Ordinal) || lower.Contains(EngineElasticsearch, StringComparison.Ordinal)) return EngineElasticsearch;
        if (lower.Contains(EngineRedshift, StringComparison.Ordinal)) return EngineRedshift;
        if (lower.Contains(EngineBigQuery, StringComparison.Ordinal)) return EngineBigQuery;
        if (lower.Contains(EngineSpanner, StringComparison.Ordinal)) return EngineSpanner;
        if (lower.Contains(EngineFirestore, StringComparison.Ordinal)) return EngineFirestore;
        if (lower.Contains(EngineBigtable, StringComparison.Ordinal)) return EngineBigtable;
        if (lower.Contains(EngineCosmosDb, StringComparison.Ordinal)) return EngineCosmosDb;
        if (lower.Contains(EngineNeptune, StringComparison.Ordinal)) return EngineNeptune;
        return null;
    }
}
