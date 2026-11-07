namespace CodeExplorer.Parser.Go.Libraries;

/// <summary>
/// Library parser for Go test frameworks (testify, ginkgo, gomega).
/// </summary>
public sealed class GoTestLibraryParser : DeclarativeSemanticExtension
{
    public static readonly GoTestLibraryParser Instance = new();

    public GoTestLibraryParser()
    {
        Id = "go-test";
        Name = "Go Test Frameworks";
        Type = SemanticExtensionType.Library;
        LibraryRole = LibraryRole.TestFramework;
        IsBuiltIn = true;
        SupportedPatterns =
        [
            "github.com/stretchr/testify*",
            "github.com/onsi/ginkgo*",
            "github.com/onsi/gomega*"
        ];
    }

    public override IEnumerable<PackageDescriptor> GetPackageDescriptors()
    {
        yield return new PackageDescriptor("testify", "Testify", LibraryRole.TestFramework, "library", ["github.com/stretchr/testify*"]);
        yield return new PackageDescriptor("ginkgo", "Ginkgo", LibraryRole.TestFramework, "library", ["github.com/onsi/ginkgo*"]);
        yield return new PackageDescriptor("gomega", "Gomega", LibraryRole.TestFramework, "library", ["github.com/onsi/gomega*"]);
    }
}

