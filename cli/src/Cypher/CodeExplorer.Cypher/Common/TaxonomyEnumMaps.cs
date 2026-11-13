using System.Runtime.CompilerServices;

namespace CodeExplorer.Cypher.Common;

public static class TaxonomyEnumExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ToCanonicalString(this DatabaseType val) => val switch
    {
        DatabaseType.Relational => "relational",
        DatabaseType.Document => "document",
        DatabaseType.KeyValue => "keyvalue",
        DatabaseType.Search => "search",
        DatabaseType.Cache => "cache",
        DatabaseType.Vector => "vector",
        DatabaseType.Graph => "graph",
        DatabaseType.Analytics => "analytics",
        DatabaseType.TimeSeries => "timeseries",
        DatabaseType.Columnar => "columnar",
        DatabaseType.MultiModel => "multimodel",
        DatabaseType.Unknown => "unknown",
        _ => "relational"
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ToCanonicalString(this MemberKind val) => val switch
    {
        MemberKind.Field => "field",
        MemberKind.Property => "property",
        MemberKind.Parameter => "parameter",
        MemberKind.Variable => "variable",
        MemberKind.Constant => "constant",
        MemberKind.Event => "event",
        MemberKind.EnumMember => "enum_member",
        MemberKind.Indexer => "indexer",
        MemberKind.Method => "method",
        MemberKind.Constructor => "constructor",
        MemberKind.Output => "output",
        _ => "field"
    };

    public static MemberKind ParseMemberKind(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return MemberKind.Field;
        return raw.Trim().ToLowerInvariant() switch
        {
            "field" => MemberKind.Field,
            "property" or "prop" => MemberKind.Property,
            "parameter" or "param" => MemberKind.Parameter,
            "variable" or "var" or "local" => MemberKind.Variable,
            "constant" or "const" => MemberKind.Constant,
            "event" => MemberKind.Event,
            "enum_member" or "enummember" => MemberKind.EnumMember,
            "indexer" => MemberKind.Indexer,
            "method" or "function" => MemberKind.Method,
            "constructor" or "ctor" => MemberKind.Constructor,
            "output" => MemberKind.Output,
            _ => MemberKind.Field
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ToCanonicalString(this TypeKind val) => val switch
    {
        TypeKind.Class => "class",
        TypeKind.Interface => "interface",
        TypeKind.Struct => "struct",
        TypeKind.Record => "record",
        TypeKind.Enum => "enum",
        TypeKind.Delegate => "delegate",
        TypeKind.Type => "type",
        TypeKind.Module => "module",
        TypeKind.Trait => "trait",
        TypeKind.Union => "union",
        TypeKind.Object => "object",
        TypeKind.Annotation => "annotation",
        TypeKind.Protocol => "protocol",
        TypeKind.Component => "component",
        TypeKind.Namespace => "namespace",
        TypeKind.Resource => "resource",
        TypeKind.DataSource => "data_source",
        TypeKind.Provider => "provider",
        TypeKind.Dependency => "dependency",
        _ => "class"
    };

    public static TypeKind ParseTypeKind(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return TypeKind.Class;
        return raw.Trim().ToLowerInvariant() switch
        {
            "class" => TypeKind.Class,
            "interface" => TypeKind.Interface,
            "struct" => TypeKind.Struct,
            "record" => TypeKind.Record,
            "enum" => TypeKind.Enum,
            "delegate" => TypeKind.Delegate,
            "type" or "typealias" or "type_alias" => TypeKind.Type,
            "module" => TypeKind.Module,
            "trait" => TypeKind.Trait,
            "union" => TypeKind.Union,
            "object" => TypeKind.Object,
            "annotation" => TypeKind.Annotation,
            "protocol" => TypeKind.Protocol,
            "component" => TypeKind.Component,
            "namespace" => TypeKind.Namespace,
            "resource" => TypeKind.Resource,
            "data_source" or "datasource" => TypeKind.DataSource,
            "provider" => TypeKind.Provider,
            "dependency" => TypeKind.Dependency,
            _ => TypeKind.Class
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ToCanonicalString(this BrokerType val) => val switch
    {
        BrokerType.RabbitMq => "rabbitmq",
        BrokerType.Kafka => "kafka",
        BrokerType.Sqs => "sqs",
        BrokerType.Sns => "sns",
        BrokerType.ServiceBus => "servicebus",
        BrokerType.AzureServiceBus => "azure-sb",
        BrokerType.Nats => "nats",
        BrokerType.Redis => "redis",
        BrokerType.InMemory => "in-memory",
        BrokerType.EventGrid => "eventgrid",
        BrokerType.PubSub => "pubsub",
        BrokerType.GcpPubSub => "gcp",
        BrokerType.MediatR => "mediatr",
        BrokerType.MassTransit => "masstransit",
        BrokerType.EventBus => "eventbus",
        BrokerType.Channel => "channel",
        BrokerType.ActiveMq => "activemq",
        BrokerType.Pulsar => "pulsar",
        BrokerType.ZeroMq => "zeromq",
        BrokerType.Mqtt => "mqtt",
        BrokerType.Event => "event",
        BrokerType.Amqp => "amqp",
        BrokerType.Spring => "spring",
        BrokerType.Kinesis => "kinesis",
        _ => "generic"
    };

    public static BrokerType ParseBrokerType(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return BrokerType.Generic;
        return raw.Trim().ToLowerInvariant() switch
        {
            "rabbitmq" or "rabbit" => BrokerType.RabbitMq,
            "kafka" => BrokerType.Kafka,
            "sqs" => BrokerType.Sqs,
            "sns" => BrokerType.Sns,
            "servicebus" or "service-bus" => BrokerType.ServiceBus,
            "azure-sb" or "azureservicebus" => BrokerType.AzureServiceBus,
            "nats" => BrokerType.Nats,
            "redis" => BrokerType.Redis,
            "in-memory" or "inmemory" or "memory" => BrokerType.InMemory,
            "eventgrid" or "event-grid" => BrokerType.EventGrid,
            "pubsub" or "pub-sub" => BrokerType.PubSub,
            "gcp" => BrokerType.GcpPubSub,
            "mediatr" => BrokerType.MediatR,
            "masstransit" => BrokerType.MassTransit,
            "eventbus" or "event-bus" => BrokerType.EventBus,
            "channel" => BrokerType.Channel,
            "activemq" or "active-mq" => BrokerType.ActiveMq,
            "pulsar" => BrokerType.Pulsar,
            "zeromq" or "zmq" => BrokerType.ZeroMq,
            "mqtt" => BrokerType.Mqtt,
            "event" => BrokerType.Event,
            "amqp" => BrokerType.Amqp,
            "spring" => BrokerType.Spring,
            "kinesis" => BrokerType.Kinesis,
            _ => BrokerType.Generic
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ToCanonicalString(this EntryPointType val) => val switch
    {
        EntryPointType.Main => "main",
        EntryPointType.MainMethod => "main-method",
        EntryPointType.TopLevel => "top-level",
        EntryPointType.ScriptMain => "script-main",
        EntryPointType.BundleEntry => "bundle-entry",
        EntryPointType.WorkerEntry => "worker-entry",
        EntryPointType.CliEntry => "cli-entry",
        EntryPointType.ServerlessHandler => "serverless-handler",
        EntryPointType.WebHost => "web-host",
        EntryPointType.TestRunner => "test-runner",
        EntryPointType.HostedService => "hosted-service",
        EntryPointType.PackageExport => "package-export",
        EntryPointType.LifecycleHook => "lifecycle-hook",
        EntryPointType.BuildHook => "build-hook",
        EntryPointType.FolderOpenTask => "folder-open-task",
        EntryPointType.Grpc => "grpc",
        EntryPointType.QueueListener => "queue-listener",
        EntryPointType.Cron => "cron",
        EntryPointType.Activity => "activity",
        EntryPointType.Provider => "provider",
        EntryPointType.Channel => "channel",
        EntryPointType.Receiver => "receiver",
        EntryPointType.Service => "service",
        EntryPointType.Cli => "cli",
        EntryPointType.Worker => "worker",
        EntryPointType.Http => "http",
        EntryPointType.WebSocket => "ws",
        EntryPointType.GraphQl => "graphql",
        EntryPointType.ScheduledJob => "scheduled-job",
        EntryPointType.UiMount => "ui-mount",
        EntryPointType.App => "app",
        EntryPointType.React => "React",
        EntryPointType.Vue => "Vue",
        EntryPointType.Angular => "Angular",
        EntryPointType.Svelte => "Svelte",
        EntryPointType.Vite => "Vite",
        EntryPointType.NextJs => "NextJs",
        EntryPointType.Nuxt => "Nuxt",
        EntryPointType.Remix => "Remix",
        EntryPointType.Astro => "Astro",
        EntryPointType.Electron => "Electron",
        EntryPointType.Subprocess => "subprocess",
        EntryPointType.Test => "test",
        EntryPointType.Benchmark => "benchmark",
        EntryPointType.Migration => "migration",
        _ => "main"
    };

    public static EntryPointType ParseEntryPointType(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return EntryPointType.Main;
        return raw.Trim().ToLowerInvariant() switch
        {
            "main" => EntryPointType.Main,
            "main-method" or "mainmethod" => EntryPointType.MainMethod,
            "top-level" or "toplevel" => EntryPointType.TopLevel,
            "script-main" or "scriptmain" => EntryPointType.ScriptMain,
            "bundle-entry" or "bundleentry" => EntryPointType.BundleEntry,
            "worker-entry" or "workerentry" => EntryPointType.WorkerEntry,
            "cli-entry" or "clientry" => EntryPointType.CliEntry,
            "serverless-handler" or "serverlesshandler" or "lambda" => EntryPointType.ServerlessHandler,
            "web-host" or "webhost" => EntryPointType.WebHost,
            "test-runner" or "testrunner" => EntryPointType.TestRunner,
            "hosted-service" or "hostedservice" => EntryPointType.HostedService,
            "package-export" or "packageexport" => EntryPointType.PackageExport,
            "lifecycle-hook" or "lifecyclehook" => EntryPointType.LifecycleHook,
            "build-hook" or "buildhook" => EntryPointType.BuildHook,
            "folder-open-task" or "folderopentask" => EntryPointType.FolderOpenTask,
            "grpc" => EntryPointType.Grpc,
            "queue-listener" or "queuelistener" => EntryPointType.QueueListener,
            "cron" => EntryPointType.Cron,
            "activity" => EntryPointType.Activity,
            "provider" => EntryPointType.Provider,
            "channel" => EntryPointType.Channel,
            "receiver" or "broadcastreceiver" => EntryPointType.Receiver,
            "service" => EntryPointType.Service,
            "cli" => EntryPointType.Cli,
            "worker" => EntryPointType.Worker,
            "http" => EntryPointType.Http,
            "ws" or "websocket" => EntryPointType.WebSocket,
            "graphql" => EntryPointType.GraphQl,
            "scheduled-job" or "scheduledjob" => EntryPointType.ScheduledJob,
            "ui-mount" or "uimount" => EntryPointType.UiMount,
            "app" or "application" => EntryPointType.App,
            "react" => EntryPointType.React,
            "vue" => EntryPointType.Vue,
            "angular" => EntryPointType.Angular,
            "svelte" => EntryPointType.Svelte,
            "vite" => EntryPointType.Vite,
            "nextjs" or "next" => EntryPointType.NextJs,
            "nuxt" => EntryPointType.Nuxt,
            "remix" => EntryPointType.Remix,
            "astro" => EntryPointType.Astro,
            "electron" => EntryPointType.Electron,
            "subprocess" => EntryPointType.Subprocess,
            "test" => EntryPointType.Test,
            "benchmark" => EntryPointType.Benchmark,
            "migration" => EntryPointType.Migration,
            _ => EntryPointType.Main
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ToCanonicalString(this DomainType val) => val switch
    {
        DomainType.Core => "Core",
        DomainType.Supporting => "Supporting",
        DomainType.Generic => "Generic",
        DomainType.Shared => "Shared",
        DomainType.External => "External",
        _ => "Core"
    };

    public static DomainType ParseDomainType(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return DomainType.Core;
        return raw.Trim().ToLowerInvariant() switch
        {
            "core" => DomainType.Core,
            "supporting" => DomainType.Supporting,
            "generic" => DomainType.Generic,
            "shared" => DomainType.Shared,
            "external" => DomainType.External,
            _ => DomainType.Core
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ToCanonicalString(this CloudResourceType val) => val switch
    {
        CloudResourceType.CloudService => "CloudService",
        CloudResourceType.S3Bucket => "S3Bucket",
        CloudResourceType.BlobStorage => "BlobStorage",
        CloudResourceType.GcsBucket => "GcsBucket",
        CloudResourceType.DynamoDb => "DynamoDb",
        CloudResourceType.CosmosDb => "CosmosDb",
        CloudResourceType.Firestore => "Firestore",
        CloudResourceType.Lambda => "Lambda",
        CloudResourceType.AzureFunction => "AzureFunction",
        CloudResourceType.CloudFunction => "CloudFunction",
        CloudResourceType.SecretsManager => "SecretsManager",
        CloudResourceType.SecretManager => "SecretManager",
        CloudResourceType.KeyVault => "KeyVault",
        CloudResourceType.ParameterStore => "ParameterStore",
        CloudResourceType.Cognito => "Cognito",
        CloudResourceType.Auth0 => "Auth0",
        CloudResourceType.Firebase => "Firebase",
        CloudResourceType.Stripe => "Stripe",
        CloudResourceType.Twilio => "Twilio",
        CloudResourceType.SendGrid => "SendGrid",
        CloudResourceType.Ses => "SES",
        CloudResourceType.Sns => "SNS",
        CloudResourceType.Sqs => "SQS",
        CloudResourceType.EventBridge => "EventBridge",
        CloudResourceType.StepFunctions => "StepFunctions",
        CloudResourceType.CloudFront => "CloudFront",
        CloudResourceType.ApiGateway => "ApiGateway",
        CloudResourceType.Kms => "KMS",
        CloudResourceType.KmsKeySet => "KmsKeySet",
        CloudResourceType.Iam => "IAM",
        _ => "CloudService"
    };

    public static CloudResourceType ParseCloudResourceType(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return CloudResourceType.CloudService;
        return raw.Trim().ToLowerInvariant() switch
        {
            "s3bucket" or "s3" => CloudResourceType.S3Bucket,
            "blobstorage" => CloudResourceType.BlobStorage,
            "gcsbucket" => CloudResourceType.GcsBucket,
            "dynamodb" => CloudResourceType.DynamoDb,
            "cosmosdb" => CloudResourceType.CosmosDb,
            "firestore" => CloudResourceType.Firestore,
            "lambda" => CloudResourceType.Lambda,
            "azurefunction" => CloudResourceType.AzureFunction,
            "cloudfunction" => CloudResourceType.CloudFunction,
            "secretsmanager" => CloudResourceType.SecretsManager,
            "secretmanager" => CloudResourceType.SecretManager,
            "keyvault" => CloudResourceType.KeyVault,
            "parameterstore" => CloudResourceType.ParameterStore,
            "cognito" => CloudResourceType.Cognito,
            "auth0" => CloudResourceType.Auth0,
            "firebase" => CloudResourceType.Firebase,
            "stripe" => CloudResourceType.Stripe,
            "twilio" => CloudResourceType.Twilio,
            "sendgrid" => CloudResourceType.SendGrid,
            "ses" => CloudResourceType.Ses,
            "sns" => CloudResourceType.Sns,
            "sqs" => CloudResourceType.Sqs,
            "eventbridge" => CloudResourceType.EventBridge,
            "stepfunctions" => CloudResourceType.StepFunctions,
            "cloudfront" => CloudResourceType.CloudFront,
            "apigateway" => CloudResourceType.ApiGateway,
            "kms" => CloudResourceType.Kms,
            "kmskeyset" => CloudResourceType.KmsKeySet,
            "iam" => CloudResourceType.Iam,
            _ => CloudResourceType.CloudService
        };
    }
}
