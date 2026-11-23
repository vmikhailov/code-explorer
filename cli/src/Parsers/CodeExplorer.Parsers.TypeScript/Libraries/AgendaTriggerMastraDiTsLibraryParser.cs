using System.Collections.Frozen;

namespace CodeExplorer.Parsers.TypeScript.Libraries;

public class AgendaTriggerMastraDiTsLibraryParser : DeclarativeSemanticExtension
{
    private const string ExtensionId = "agenda-trigger-mastra-di-ts";
    private const string ExtensionName = "Agenda, Trigger.dev, Mastra AI, Resend/Nodemailer & Inversify/TSyringe/TypeDI/Awilix";
    private const string ProtocolTask = "ts-worker";
    private const string FallbackTask = "background-task";

    public AgendaTriggerMastraDiTsLibraryParser()
    {
        Id = ExtensionId;
        Name = ExtensionName;
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.WorkerService;
        SupportedPatterns =
        [
            "agenda*",
            "@hokify/agenda*",
            "@trigger.dev/*",
            "@mastra/*",
            "mastra*",
            "resend*",
            "nodemailer*",
            "inversify*",
            "tsyringe*",
            "typedi*",
            "awilix*",
            "@upstash/redis*",
            "@upstash/qstash*",
            "postgres",
            "better-sqlite3*",
            "@typegoose/*",
            "objection*",
            "mercurius*",
            "type-graphql*",
            "@connectrpc/*",
            "ofetch*",
            "lucia*",
            "better-auth*",
            "@clerk/*",
            "msw*",
            "supertest*",
            "pino*",
            "winston*",
            "@sentry/*",
            "@opentelemetry/*",
            "rxjs*",
            "jsonwebtoken*",
            "passport*",
            "cors*",
            "body-parser*",
            "cookie*",
            "multer*",
            "compression*",
            "ajv*",
            "tedious*",
            "uWebSockets.js*",
            "@envelop/*",
            "@apollo/*",
            "@pothos/*",
            "@pulumi/*",
            "jose*",
            "sharp*",
            "class-validator*",
            "class-transformer*",
            "bcrypt*",
            "argon2*",
            "helmet*",
            "morgan*",
            "@boxyhq/*"
        ];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = ProtocolTask,
                PublishMethodNames = new[]
                {
                    "now", "every", "schedule", "trigger", "batchTrigger", "sendTask"
                }.ToFrozenSet(StringComparer.Ordinal),
                PublishReceiverSubstrings = new[]
                {
                    "agenda", "tasks", "qstash", "queue"
                }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                SubscribeMethodNames = new[] { "define", "task" }.ToFrozenSet(StringComparer.Ordinal),
                SubscribeReceiverSubstrings = new[] { "agenda", "tasks" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                FallbackTopicName = FallbackTask
            }
        ];
        ServiceCallSinkRules =
        [
            new ServiceCallSinkRule
            {
                CallMethodNames = new[]
                {
                    "sendMail", "generate", "stream"
                }.ToFrozenSet(StringComparer.Ordinal),
                ReceiverSubstrings = new[] { "transporter", "mailer", "emails", "resend", "agent", "mastra" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                TargetRelation = OntologyConstants.Relationships.Calls,
                StaticTargetName = "external:mail-or-ai"
            }
        ];
    }
}
