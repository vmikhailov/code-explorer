using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Python.Libraries;

/// <summary>
/// Library parser for Python test frameworks (pytest, unittest, hypothesis).
/// </summary>
public sealed class PythonTestLibraryParser : ISemanticExtension
{
    public static readonly PythonTestLibraryParser Instance = new();

    public string Id => "python-test";
    public string Name => "Python Test Frameworks";
    public string Type => "library";
    public LibraryRole LibraryRole => LibraryRole.TestFramework;

    public IReadOnlyList<string> SupportedPatterns =>
    [
        "pytest", "pytest-*", "pytest_*",
        "unittest", "unittest2",
        "hypothesis"
    ];

    public bool IsImplemented => true;
    public bool IsBuiltIn => true;

    public IEnumerable<PackageDescriptor> GetPackageDescriptors()
    {
        yield return new PackageDescriptor("pytest", "Pytest", LibraryRole.TestFramework, "library", ["pytest", "pytest-*", "pytest_*"]);
        yield return new PackageDescriptor("unittest", "Unittest", LibraryRole.TestFramework, "library", ["unittest", "unittest2"]);
        yield return new PackageDescriptor("hypothesis", "Hypothesis", LibraryRole.TestFramework, "library", ["hypothesis"]);
    }

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;
}

