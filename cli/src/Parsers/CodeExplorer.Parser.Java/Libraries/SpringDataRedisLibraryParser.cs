namespace CodeExplorer.Parser.Java.Libraries;

public class SpringDataRedisLibraryParser : DeclarativeSemanticExtension
{
    private const string ExtensionId = "spring-data-redis";
    private const string ExtensionName = "Spring Data Redis";

    public SpringDataRedisLibraryParser()
    {
        Id = ExtensionId;
        Name = ExtensionName;
        Type = SemanticExtensionType.KeyValueDb;
        PackageDescriptorTypeOverride = SemanticExtensionType.Library;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns =
        [
            "org.springframework.boot.spring-boot-starter-data-redis*",
            "org.springframework.data.spring-data-redis*",
            "org.springframework.data.redis*",
            "*data-redis*",
            "*redis*",
            "redis.clients.jedis*",
            "io.lettuce*"
        ];
    }
}
