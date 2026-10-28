using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Java.Libraries;

/// <summary>
/// Library parser for Java test frameworks (JUnit, TestNG, Spock, Spring Boot Test, AssertJ).
/// </summary>
public sealed class JavaTestLibraryParser : ISemanticExtension
{
    public static readonly JavaTestLibraryParser Instance = new();

    public string Id => "java-test";
    public string Name => "Java Test Frameworks";
    public string Type => "library";
    public LibraryRole LibraryRole => LibraryRole.TestFramework;

    public IReadOnlyList<string> SupportedPatterns =>
    [
        "junit", "org.junit.*",
        "testng", "org.testng*",
        "org.spockframework*",
        "org.springframework.boot:spring-boot-starter-test", "spring-boot-starter-test",
        "org.assertj*"
    ];

    public bool IsImplemented => true;
    public bool IsBuiltIn => true;

    public IEnumerable<PackageDescriptor> GetPackageDescriptors()
    {
        yield return new PackageDescriptor("junit", "JUnit", LibraryRole.TestFramework, "library", ["junit", "org.junit.*"]);
        yield return new PackageDescriptor("testng", "TestNG", LibraryRole.TestFramework, "library", ["testng", "org.testng*"]);
        yield return new PackageDescriptor("spock", "Spock", LibraryRole.TestFramework, "library", ["org.spockframework*"]);
        yield return new PackageDescriptor("spring-boot-test", "Spring Boot Test", LibraryRole.TestFramework, "library", ["org.springframework.boot:spring-boot-starter-test", "spring-boot-starter-test"]);
        yield return new PackageDescriptor("assertj", "AssertJ", LibraryRole.TestFramework, "library", ["org.assertj*"]);
    }

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;
}

