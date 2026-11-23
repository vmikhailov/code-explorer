using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Parsers.Kotlin.Libraries;

public sealed class SpringBootKotlinLibraryParser : DeclarativeSemanticExtension
{
    private const string MarkerSpring = "org.springframework";
    private const string MarkerWebClient = "WebClient";
    private const string HttpPrefix = "http:";
    private const string RedisPrefix = "redis";

    private static readonly Regex RouterDslRegex = new(
        @"\b(?<verb>GET|POST|PUT|DELETE|PATCH)\s*\(\s*[""'](?<path>/[^""']*)[""']",
        RegexOptions.Compiled);

    private static readonly Regex WebClientCallRegex = new(
        @"\.\s*uri\s*\(\s*[""'](?<url>[^""']+)[""']",
        RegexOptions.Compiled);

    public SpringBootKotlinLibraryParser()
    {
        Id = "spring-boot-kotlin";
        Name = "Spring Boot, WebFlux Router DSL, Spring Data & Security (Kotlin)";
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns =
        [
            "org.springframework.boot*",
            "org.springframework.web*",
            "org.springframework.data*",
            "org.springframework.security*",
            "io.projectreactor*",
            "jakarta.persistence*",
            "com.h2database*",
            "org.flywaydb*",
            "org.liquibase*",
            "com.github.ben-manes.caffeine*",
            "org.testcontainers*",
            "com.ninja-squad:springmockk*"
        ];
    }

    public static void AnalyzeSource(string content, FileNode fileNode)
    {
        if (!content.Contains(MarkerSpring, StringComparison.Ordinal))
        {
            return;
        }

        foreach (Match match in RouterDslRegex.Matches(content))
        {
            var verb = match.Groups[1].Value.Trim().ToUpperInvariant();
            var path = match.Groups[2].Value.Trim();
            if (!string.IsNullOrEmpty(path))
            {
                fileNode.References.Add(new Reference(fileNode.Id, $"{verb}:{path}", OntologyConstants.Relationships.Exposes));
            }
        }

        if (content.Contains(MarkerWebClient, StringComparison.Ordinal) ||
            content.Contains("RestClient", StringComparison.Ordinal))
        {
            foreach (Match match in WebClientCallRegex.Matches(content))
            {
                var url = match.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(url))
                {
                    fileNode.References.Add(new Reference(fileNode.Id, HttpPrefix + url, OntologyConstants.Relationships.Calls));
                }
            }
        }

        if (content.Contains("RedisTemplate", StringComparison.Ordinal) ||
            content.Contains("StringRedisTemplate", StringComparison.Ordinal) ||
            content.Contains("ReactiveRedisTemplate", StringComparison.Ordinal))
        {
            fileNode.References.Add(new Reference(fileNode.Id, RedisPrefix, OntologyConstants.Relationships.UsesDb));
        }
    }
}
