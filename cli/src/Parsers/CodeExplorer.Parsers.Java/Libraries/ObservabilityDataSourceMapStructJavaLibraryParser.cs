using System.Collections.Frozen;

namespace CodeExplorer.Parsers.Java.Libraries;

public class ObservabilityDataSourceMapStructJavaLibraryParser : DeclarativeSemanticExtension
{
    private const string ExtensionId = "observability-datasource-mapstruct-java";
    private const string ExtensionName = "Micrometer, OpenTelemetry Java, HikariCP/Druid/H2, MapStruct, OpenAI-Java & WireMock/RestAssured/Testcontainers";

    public ObservabilityDataSourceMapStructJavaLibraryParser()
    {
        Id = ExtensionId;
        Name = ExtensionName;
        Type = SemanticExtensionType.Library;
        LibraryRole = LibraryRole.General;
        SupportedPatterns =
        [
            "io.micrometer*",
            "io.opentelemetry*",
            "com.netflix.spectator*",
            "org.apache.skywalking*",
            "com.zaxxer:HikariCP*",
            "com.zaxxer.hikari*",
            "com.alibaba:druid*",
            "com.alibaba.druid*",
            "com.mchange:c3p0*",
            "com.h2database*",
            "org.flywaydb*",
            "org.liquibase*",
            "org.mapstruct*",
            "com.openai*",
            "org.testcontainers*",
            "org.wiremock*",
            "com.github.tomakehurst:wiremock*",
            "io.rest-assured*",
            "com.tngtech.archunit*",
            "com.ctrip.framework.apollo*",
            "com.alibaba.nacos*",
            "com.hazelcast*",
            "org.ehcache*",
            "org.asynchttpclient*",
            "com.konghq:unirest*"
        ];
        AnnotationSinkRules =
        [
            new AnnotationSinkRule
            {
                AnnotationNames = new[]
                {
                    "Timed", "Counted", "Observed", "WithSpan", "Mapper", "ApolloConfig", "NacosValue"
                }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                TargetPrefix = "jvm-infra:",
                TargetRelation = OntologyConstants.Relationships.Calls
            }
        ];
        ServiceCallSinkRules =
        [
            new ServiceCallSinkRule
            {
                CallMethodNames = new[]
                {
                    "counter", "timer", "gauge", "spanBuilder", "migrate"
                }.ToFrozenSet(StringComparer.Ordinal),
                ReceiverSubstrings = new[]
                {
                    "meterRegistry", "registry", "tracer", "flyway", "liquibase"
                }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                TargetRelation = OntologyConstants.Relationships.Calls,
                StaticTargetName = "jvm-observability:telemetry"
            }
        ];
    }
}
