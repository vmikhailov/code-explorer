using CodeExplorer.Core.Parser;
using CodeExplorer.Parser.TypeScript.Libraries;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class ConstantRegistryTests
{
    [SetUp]
    public void SetUp()
    {
        ConstantRegistry.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        ConstantRegistry.Clear();
    }

    [Test]
    public void TypeScript_EnumDeclarations_AreScannedAndResolved()
    {
        var code = """
        export enum Schemas {
            Defaults = 'defaults',
            Sources = 'sources',
            Networks = 'networks',
            TreeRules = 'tree_rules'
        }

        enum ModelNames {
            SourcesPlacementsBlack = 'source_placements_black',
            SourceRules = 'source_rules',
            Sources = 'sources'
        }
        """;

        ConstantRegistry.ScanAndRegister("sources/src/shared/constants/enums.ts", code, "sources");

        // Project-scoped lookup
        Assert.That(ConstantRegistry.TryResolve("sources", "Schemas.Sources", out var schemaVal), Is.True);
        Assert.That(schemaVal, Is.EqualTo("sources"));

        Assert.That(ConstantRegistry.TryResolve("sources", "ModelNames.SourcesPlacementsBlack", out var modelVal), Is.True);
        Assert.That(modelVal, Is.EqualTo("source_placements_black"));

        // Path-based lookup
        Assert.That(ConstantRegistry.TryResolve("sources/src/entities/source.entity.ts", "Schemas.TreeRules", out var schemaFromPath), Is.True);
        Assert.That(schemaFromPath, Is.EqualTo("tree_rules"));

        // Global fallback
        Assert.That(ConstantRegistry.TryResolve(null, "ModelNames.SourceRules", out var globalVal), Is.True);
        Assert.That(globalVal, Is.EqualTo("source_rules"));

        // Unqualified member fallback
        Assert.That(ConstantRegistry.TryResolve(null, "SourcesPlacementsBlack", out var unqualifiedVal), Is.True);
        Assert.That(unqualifiedVal, Is.EqualTo("source_placements_black"));
    }

    [Test]
    public void TypeScript_ConstObjects_AreScannedAndResolved()
    {
        var code = """
        export const ETables = {
            USER_CONFIGS: 'user_configs',
            TRACKER_SAVED_STATES: 'tracker_saved_states',
            TOP_PLACEMENTS_QUERIES: 'top_placements_queries'
        } as const;

        const ENTITY_NAME = {
            FULL_BUNDLES: 'full_bundles',
            BUNDLES: 'bundles'
        };
        """;

        ConstantRegistry.ScanAndRegister("bff/src/shared/enums.ts", code, "bff");

        Assert.That(ConstantRegistry.TryResolve("bff", "ETables.TOP_PLACEMENTS_QUERIES", out var tableVal), Is.True);
        Assert.That(tableVal, Is.EqualTo("top_placements_queries"));

        Assert.That(ConstantRegistry.TryResolve(null, "ENTITY_NAME.BUNDLES", out var bundleVal), Is.True);
        Assert.That(bundleVal, Is.EqualTo("bundles"));
    }

    [Test]
    public void CSharp_StaticClassConstants_AreScannedAndResolved()
    {
        var code = """
        namespace MyApp.Infrastructure;

        public static class TableNames
        {
            public const string Users = "users";
            public const string Orders = "orders";
            public const string OrderDetails = @"order_details";
        }
        """;

        ConstantRegistry.ScanAndRegister("OrderService/Data/TableNames.cs", code, "OrderService");

        Assert.That(ConstantRegistry.TryResolve("OrderService", "TableNames.Users", out var usersVal), Is.True);
        Assert.That(usersVal, Is.EqualTo("users"));

        Assert.That(ConstantRegistry.TryResolve(null, "TableNames.OrderDetails", out var detailsVal), Is.True);
        Assert.That(detailsVal, Is.EqualTo("order_details"));
    }

    [Test]
    public void Go_ConstBlocks_AreScannedAndResolved()
    {
        var code = """
        package repository

        const (
            BundlesTable = "bundles"
            SnapshotsTable = "snapshots"
        )

        const RoutingTable string = "routing"
        """;

        ConstantRegistry.ScanAndRegister("aggregator/internal/repo/tables.go", code, "cpm-streaming-aggregator");

        Assert.That(ConstantRegistry.TryResolve("cpm-streaming-aggregator", "BundlesTable", out var bVal), Is.True);
        Assert.That(bVal, Is.EqualTo("bundles"));

        Assert.That(ConstantRegistry.TryResolve(null, "RoutingTable", out var rVal), Is.True);
        Assert.That(rVal, Is.EqualTo("routing"));
    }

    [Test]
    public void NestedSql_CleanQueryText_InterpolatesResolvedConstants()
    {
        ConstantRegistry.Register(null, "Schemas.Networks", "networks");
        ConstantRegistry.Register(null, "ModelNames.Networks", "networks_table");

        var rawSql = "ALTER TABLE ${Schemas.Networks}.${ModelNames.Networks} ADD COLUMN field varchar(50);";
        var cleaned = NestedSqlParser.CleanQueryText(rawSql);

        Assert.That(cleaned, Does.Contain("ALTER TABLE networks.networks_table ADD COLUMN field varchar(50);"));
    }

    [Test]
    public void SyntaxEnricher_DetectDeclaredSchema_ResolvesUnquotedTypeOrmSchema()
    {
        ConstantRegistry.Register("sources", "Schemas.Sources", "sources");

        var dummyFilePath = Path.Combine(Path.GetTempPath(), $"schema_test_{Guid.NewGuid():N}.ts");
        try
        {
            File.WriteAllText(dummyFilePath, """
            import { Entity } from 'typeorm';
            import { Schemas } from './enums';

            @Entity({ name: 'my_table', schema: Schemas.Sources })
            export class MyTableEntity {}
            """);

            var fileNode = new CodeExplorer.Core.Common.Nodes.Layer1_Physical.FileNode("f1", "my_table.entity.ts", "my_table.entity.ts", dummyFilePath);
            var tsParser = new CodeExplorer.Parser.TypeScript.TypeScriptParser();
            var syntaxTree = new SyntaxTree(dummyFilePath, "test", null, null, null, fileNode, tsParser, [], [], []);
            var detected = SyntaxEnricher.DetectDeclaredSchema(null, null, syntaxTree);

            Assert.That(detected, Is.EqualTo("sources"));
        }
        finally
        {
            if (File.Exists(dummyFilePath)) File.Delete(dummyFilePath);
        }
    }

    [Test]
    public void DotEnv_File_IsScannedAndRegistered()
    {
        var envContent = """
        # Comments
        export EVENT_BUS_TOPIC_NAME="custom-event-bus-topic"
        RULE_TREE_TOPIC=rule-tree-updates # inline comment
        DB_HOST=127.0.0.1
        WEBHOOK_SECRET="hash#value=123"
        IGNORED_LINE_WITHOUT_EQUALS
        """;

        ConstantRegistry.ScanAndRegister("path/to/.env", envContent, "billing");

        Assert.That(ConstantRegistry.TryResolve("billing", "EVENT_BUS_TOPIC_NAME", out var topicVal), Is.True);
        Assert.That(topicVal, Is.EqualTo("custom-event-bus-topic"));

        Assert.That(ConstantRegistry.TryResolve("billing", "RULE_TREE_TOPIC", out var ruleVal), Is.True);
        Assert.That(ruleVal, Is.EqualTo("rule-tree-updates"));

        Assert.That(ConstantRegistry.TryResolve("billing", "WEBHOOK_SECRET", out var secretVal), Is.True);
        Assert.That(secretVal, Is.EqualTo("hash#value=123"));
    }

    [Test]
    public void TryDeriveTopicOrQueueFromEnvVar_DerivesExpectedTopicNames()
    {
        // 1. Subscription -> Topic derivation
        Assert.That(ConstantRegistry.TryDeriveTopicOrQueueFromEnvVar("EVENT_BUS_SUBSCRIPTION_NAME", out var fromSub), Is.True);
        Assert.That(fromSub, Is.EqualTo("event-bus-topic"));

        Assert.That(ConstantRegistry.TryDeriveTopicOrQueueFromEnvVar("ORDER_EVENTS_SUB", out var fromOrderSub), Is.True);
        Assert.That(fromOrderSub, Is.EqualTo("order-events-topic"));

        // 2. Topic/Queue -> Kebab-case topic derivation
        Assert.That(ConstantRegistry.TryDeriveTopicOrQueueFromEnvVar("EVENT_BUS_TOPIC_NAME", out var fromTopicName), Is.True);
        Assert.That(fromTopicName, Is.EqualTo("event-bus-topic"));

        Assert.That(ConstantRegistry.TryDeriveTopicOrQueueFromEnvVar("PAYMENT_QUEUE", out var fromQueue), Is.True);
        Assert.That(fromQueue, Is.EqualTo("payment-queue"));

        // 3. Dummy / placeholder names should fail derivation
        Assert.That(ConstantRegistry.TryDeriveTopicOrQueueFromEnvVar("TOPIC_NAME", out _), Is.False);
        Assert.That(ConstantRegistry.TryDeriveTopicOrQueueFromEnvVar("QUEUE_NAME", out _), Is.False);

        // 4. Non-messaging variables (URLs, tables, secrets, flags) MUST fail derivation
        Assert.That(ConstantRegistry.TryDeriveTopicOrQueueFromEnvVar("BILLING_SUBSCRIPTION_URL", out _), Is.False);
        Assert.That(ConstantRegistry.TryDeriveTopicOrQueueFromEnvVar("USER_SUBSCRIPTIONS_TABLE", out _), Is.False);
        Assert.That(ConstantRegistry.TryDeriveTopicOrQueueFromEnvVar("STRIPE_SUBSCRIPTION_WEBHOOK_SECRET", out _), Is.False);
        Assert.That(ConstantRegistry.TryDeriveTopicOrQueueFromEnvVar("SUBSCRIPTION_ENABLED", out _), Is.False);
        Assert.That(ConstantRegistry.TryDeriveTopicOrQueueFromEnvVar("SUBSCRIBERS_COUNT", out _), Is.False);
    }

    [Test]
    public void WorkspaceConventions_NormalizeTopicName_FiltersPlaceholdersAndSubscriptions()
    {
        Assert.That(CodeExplorer.Core.Common.WorkspaceConventions.NormalizeTopicName("TOPIC_NAME"), Is.Empty);
        Assert.That(CodeExplorer.Core.Common.WorkspaceConventions.NormalizeTopicName("QUEUE_NAME"), Is.Empty);
        Assert.That(CodeExplorer.Core.Common.WorkspaceConventions.NormalizeTopicName("EVENT_SUBSCRIBER_NAME"), Is.Empty);
        Assert.That(CodeExplorer.Core.Common.WorkspaceConventions.NormalizeTopicName("default-sub-id"), Is.Empty);
        Assert.That(CodeExplorer.Core.Common.WorkspaceConventions.NormalizeTopicName("default-subscription-name"), Is.Empty);

        Assert.That(CodeExplorer.Core.Common.WorkspaceConventions.NormalizeTopicName("ORDER_EVENTS_TOPIC"), Is.EqualTo("order-events-topic"));
    }

    [Test]
    public void WorkspaceConventions_IsPlaceholderName_DetectsPlaceholdersAccurately()
    {
        Assert.That(CodeExplorer.Core.Common.WorkspaceConventions.IsPlaceholderName("TOPIC_NAME"), Is.True);
        Assert.That(CodeExplorer.Core.Common.WorkspaceConventions.IsPlaceholderName("QUEUE_NAME"), Is.True);
        Assert.That(CodeExplorer.Core.Common.WorkspaceConventions.IsPlaceholderName("default-topic"), Is.True);
        Assert.That(CodeExplorer.Core.Common.WorkspaceConventions.IsPlaceholderName("default-sub-id"), Is.True);

        Assert.That(CodeExplorer.Core.Common.WorkspaceConventions.IsPlaceholderName("order-events-topic"), Is.False);
        Assert.That(CodeExplorer.Core.Common.WorkspaceConventions.IsPlaceholderName("payment-queue"), Is.False);
    }
}
