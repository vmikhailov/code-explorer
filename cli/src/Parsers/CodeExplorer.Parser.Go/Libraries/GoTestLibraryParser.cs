using CodeExplorer.Core.Parser;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Go.Libraries;

/// <summary>
/// Library parser for Go test frameworks (testify, ginkgo, gomega).
/// </summary>
public sealed class GoTestLibraryParser : ISemanticExtension
{
    public static readonly GoTestLibraryParser Instance = new();

    public string Id => "go-test";
    public string Name => "Go Test Frameworks";
    public string Type => "library";
    public LibraryRole LibraryRole => LibraryRole.TestFramework;

    public IReadOnlyList<string> SupportedPatterns =>
    [
        "github.com/stretchr/testify*",
        "github.com/onsi/ginkgo*",
        "github.com/onsi/gomega*"
    ];

    public bool IsImplemented => true;
    public bool IsBuiltIn => true;

    public IEnumerable<PackageDescriptor> GetPackageDescriptors()
    {
        yield return new PackageDescriptor("testify", "Testify", LibraryRole.TestFramework, "library", ["github.com/stretchr/testify*"]);
        yield return new PackageDescriptor("ginkgo", "Ginkgo", LibraryRole.TestFramework, "library", ["github.com/onsi/ginkgo*"]);
        yield return new PackageDescriptor("gomega", "Gomega", LibraryRole.TestFramework, "library", ["github.com/onsi/gomega*"]);
    }

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;
}

