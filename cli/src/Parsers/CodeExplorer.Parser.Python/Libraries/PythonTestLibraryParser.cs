namespace CodeExplorer.Parser.Python.Libraries;

/// <summary>
/// Library parser for Python test frameworks (pytest, unittest, hypothesis).
/// </summary>
public sealed class PythonTestLibraryParser : DeclarativeSemanticExtension
{
    public static readonly PythonTestLibraryParser Instance = new();

    public PythonTestLibraryParser()
    {
        Id = "python-test";
        Name = "Python Test Frameworks";
        Type = SemanticExtensionType.Library;
        LibraryRole = LibraryRole.TestFramework;
        IsBuiltIn = true;
        SupportedPatterns =
        [
            "pytest", "pytest-*", "pytest_*",
            "unittest", "unittest2",
            "hypothesis"
        ];
    }

    public override IEnumerable<PackageDescriptor> GetPackageDescriptors()
    {
        yield return new PackageDescriptor("pytest", "Pytest", LibraryRole.TestFramework, "library", ["pytest", "pytest-*", "pytest_*"]);
        yield return new PackageDescriptor("unittest", "Unittest", LibraryRole.TestFramework, "library", ["unittest", "unittest2"]);
        yield return new PackageDescriptor("hypothesis", "Hypothesis", LibraryRole.TestFramework, "library", ["hypothesis"]);
    }
}

