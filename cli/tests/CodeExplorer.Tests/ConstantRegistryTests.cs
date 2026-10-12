using AwesomeAssertions;
using CodeExplorer.Core.Parser;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
[NonParallelizable]
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
        ConstantRegistry.TryResolve("sources", "Schemas.Sources", out var schemaVal).Should().BeTrue();
        schemaVal.Should().Be("sources");

        ConstantRegistry.TryResolve("sources", "ModelNames.SourcesPlacementsBlack", out var modelVal).Should().BeTrue();
        modelVal.Should().Be("source_placements_black");

        // Path-based lookup
        ConstantRegistry.TryResolve("sources/src/entities/source.entity.ts", "Schemas.TreeRules", out var schemaFromPath).Should().BeTrue();
        schemaFromPath.Should().Be("tree_rules");

        // Global fallback
        ConstantRegistry.TryResolve(null, "ModelNames.SourceRules", out var globalVal).Should().BeTrue();
        globalVal.Should().Be("source_rules");

        // Unqualified member fallback
        ConstantRegistry.TryResolve(null, "SourcesPlacementsBlack", out var unqualifiedVal).Should().BeTrue();
        unqualifiedVal.Should().Be("source_placements_black");
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

        ConstantRegistry.TryResolve("bff", "ETables.TOP_PLACEMENTS_QUERIES", out var tableVal).Should().BeTrue();
        tableVal.Should().Be("top_placements_queries");

        ConstantRegistry.TryResolve(null, "ENTITY_NAME.BUNDLES", out var bundleVal).Should().BeTrue();
        bundleVal.Should().Be("bundles");
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

        ConstantRegistry.TryResolve("OrderService", "TableNames.Users", out var usersVal).Should().BeTrue();
        usersVal.Should().Be("users");

        ConstantRegistry.TryResolve(null, "TableNames.OrderDetails", out var detailsVal).Should().BeTrue();
        detailsVal.Should().Be("order_details");
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

        ConstantRegistry.TryResolve("cpm-streaming-aggregator", "BundlesTable", out var bVal).Should().BeTrue();
        bVal.Should().Be("bundles");

        ConstantRegistry.TryResolve(null, "RoutingTable", out var rVal).Should().BeTrue();
        rVal.Should().Be("routing");
    }

    [Test]
    public void NestedSql_CleanQueryText_InterpolatesResolvedConstants()
    {
        ConstantRegistry.Register(null, "Schemas.Networks", "networks");
        ConstantRegistry.Register(null, "ModelNames.Networks", "networks_table");

        var rawSql = "ALTER TABLE ${Schemas.Networks}.${ModelNames.Networks} ADD COLUMN field varchar(50);";
        var cleaned = NestedSqlParser.CleanQueryText(rawSql);

        cleaned.Should().Contain("ALTER TABLE networks.networks_table ADD COLUMN field varchar(50);");
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
            var tsParser = new Parser.TypeScript.TypeScriptParser();
            var syntaxTree = new SyntaxTree(dummyFilePath, "test", null, null, null, fileNode, tsParser, [], [], []);
            var detected = SyntaxEnricher.DetectDeclaredSchema(null, null, syntaxTree);

            detected.Should().Be("sources");
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

        ConstantRegistry.TryResolve("billing", "EVENT_BUS_TOPIC_NAME", out var topicVal).Should().BeTrue();
        topicVal.Should().Be("custom-event-bus-topic");

        ConstantRegistry.TryResolve("billing", "RULE_TREE_TOPIC", out var ruleVal).Should().BeTrue();
        ruleVal.Should().Be("rule-tree-updates");

        ConstantRegistry.TryResolve("billing", "WEBHOOK_SECRET", out var secretVal).Should().BeTrue();
        secretVal.Should().Be("hash#value=123");
    }

    [Test]
    public void TryDeriveTopicOrQueueFromEnvVar_DerivesExpectedTopicNames()
    {
        // 1. Subscription -> Topic derivation
        ConstantRegistry.TryDeriveTopicOrQueueFromEnvVar("EVENT_BUS_SUBSCRIPTION_NAME", out var fromSub).Should().BeTrue();
        fromSub.Should().Be("event-bus-topic");

        ConstantRegistry.TryDeriveTopicOrQueueFromEnvVar("ORDER_EVENTS_SUB", out var fromOrderSub).Should().BeTrue();
        fromOrderSub.Should().Be("order-events-topic");

        // 2. Topic/Queue -> Kebab-case topic derivation
        ConstantRegistry.TryDeriveTopicOrQueueFromEnvVar("EVENT_BUS_TOPIC_NAME", out var fromTopicName).Should().BeTrue();
        fromTopicName.Should().Be("event-bus-topic");

        ConstantRegistry.TryDeriveTopicOrQueueFromEnvVar("PAYMENT_QUEUE", out var fromQueue).Should().BeTrue();
        fromQueue.Should().Be("payment-queue");

        // 3. Dummy / placeholder names should fail derivation
        ConstantRegistry.TryDeriveTopicOrQueueFromEnvVar("TOPIC_NAME", out _).Should().BeFalse();
        ConstantRegistry.TryDeriveTopicOrQueueFromEnvVar("QUEUE_NAME", out _).Should().BeFalse();
        ConstantRegistry.TryDeriveTopicOrQueueFromEnvVar("NETWORK_TOPIC", out _).Should().BeFalse();

        // 4. Non-messaging variables (URLs, tables, secrets, flags) MUST fail derivation
        ConstantRegistry.TryDeriveTopicOrQueueFromEnvVar("BILLING_SUBSCRIPTION_URL", out _).Should().BeFalse();
        ConstantRegistry.TryDeriveTopicOrQueueFromEnvVar("USER_SUBSCRIPTIONS_TABLE", out _).Should().BeFalse();
        ConstantRegistry.TryDeriveTopicOrQueueFromEnvVar("STRIPE_SUBSCRIPTION_WEBHOOK_SECRET", out _).Should().BeFalse();
        ConstantRegistry.TryDeriveTopicOrQueueFromEnvVar("SUBSCRIPTION_ENABLED", out _).Should().BeFalse();
        ConstantRegistry.TryDeriveTopicOrQueueFromEnvVar("SUBSCRIBERS_COUNT", out _).Should().BeFalse();
    }

    [Test]
    public void WorkspaceConventions_NormalizeTopicName_FiltersPlaceholdersAndSubscriptions()
    {
        Core.Common.WorkspaceConventions.NormalizeTopicName("TOPIC_NAME").Should().BeEmpty();
        Core.Common.WorkspaceConventions.NormalizeTopicName("QUEUE_NAME").Should().BeEmpty();
        Core.Common.WorkspaceConventions.NormalizeTopicName("EVENT_SUBSCRIBER_NAME").Should().BeEmpty();
        Core.Common.WorkspaceConventions.NormalizeTopicName("default-sub-id").Should().BeEmpty();
        Core.Common.WorkspaceConventions.NormalizeTopicName("default-subscription-name").Should().BeEmpty();
        Core.Common.WorkspaceConventions.NormalizeTopicName("NETWORK_TOPIC").Should().BeEmpty();

        Core.Common.WorkspaceConventions.NormalizeTopicName("ORDER_EVENTS_TOPIC").Should().Be("order-events-topic");
    }

    [Test]
    public void WorkspaceConventions_IsPlaceholderName_DetectsPlaceholdersAccurately()
    {
        Core.Common.WorkspaceConventions.IsPlaceholderName("TOPIC_NAME").Should().BeTrue();
        Core.Common.WorkspaceConventions.IsPlaceholderName("QUEUE_NAME").Should().BeTrue();
        Core.Common.WorkspaceConventions.IsPlaceholderName("default-topic").Should().BeTrue();
        Core.Common.WorkspaceConventions.IsPlaceholderName("default-sub-id").Should().BeTrue();
        Core.Common.WorkspaceConventions.IsPlaceholderName("network-topic").Should().BeTrue();

        Core.Common.WorkspaceConventions.IsPlaceholderName("order-events-topic").Should().BeFalse();
        Core.Common.WorkspaceConventions.IsPlaceholderName("payment-queue").Should().BeFalse();
    }

    [Test]
    public void WorkspaceConventions_CustomConfiguredPlaceholders_AreRecognized()
    {
        try
        {
            Core.Common.WorkspaceConventions.Current = new Core.Conventions.WorkspaceConventionProfile(
                new Core.Conventions.Services.ServiceIdentityResolver(),
                new Core.Conventions.Services.MessagingConventionService(customPlaceholders: ["custom-placeholder-token", "google-pub-sub-network-topic"]),
                new Core.Conventions.Services.DomainTaxonomyEngine(),
                new Core.Conventions.Services.NetworkEntityValidator()
            );

            Core.Common.WorkspaceConventions.IsPlaceholderName("custom-placeholder-token").Should().BeTrue();
            Core.Common.WorkspaceConventions.IsPlaceholderName("google-pub-sub-network-topic").Should().BeTrue();
            Core.Common.WorkspaceConventions.NormalizeTopicName("GOOGLE_PUB_SUB_NETWORK_TOPIC").Should().BeEmpty();
        }
        finally
        {
            Core.Common.WorkspaceConventions.Clear();
        }
    }
}
