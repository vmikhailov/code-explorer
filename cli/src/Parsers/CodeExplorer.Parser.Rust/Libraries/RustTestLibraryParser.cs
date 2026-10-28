using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Rust.Libraries;

/// <summary>
/// Library parser for Rust test and benchmark frameworks (criterion, divan, proptest, quickcheck, rstest, insta, mockall, wiremock).
/// </summary>
public sealed class RustTestLibraryParser : ISemanticExtension
{
    public static readonly RustTestLibraryParser Instance = new();

    public string Id => "rust-test";
    public string Name => "Rust Test Frameworks";
    public string Type => "library";
    public LibraryRole LibraryRole => LibraryRole.TestFramework;

    public IReadOnlyList<string> SupportedPatterns =>
    [
        "criterion", "divan",
        "proptest", "quickcheck",
        "rstest", "test-case", "insta", "mockall", "wiremock", "serial_test", "tokio-test"
    ];

    public bool IsImplemented => true;
    public bool IsBuiltIn => true;

    public IEnumerable<PackageDescriptor> GetPackageDescriptors()
    {
        yield return new PackageDescriptor("criterion", "Criterion / Divan", LibraryRole.TestFramework, "library", ["criterion", "divan"]);
        yield return new PackageDescriptor("proptest", "Proptest / Quickcheck", LibraryRole.TestFramework, "library", ["proptest", "quickcheck"]);
        yield return new PackageDescriptor("rstest", "Rust Test Utilities", LibraryRole.TestFramework, "library", ["rstest", "test-case", "insta", "mockall", "wiremock", "serial_test", "tokio-test"]);
    }

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;
}

