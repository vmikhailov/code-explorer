namespace CodeExplorer.Parser.Rust.Libraries;

/// <summary>
/// Library parser for Rust test and benchmark frameworks (criterion, divan, proptest, quickcheck, rstest, insta, mockall, wiremock).
/// </summary>
public sealed class RustTestLibraryParser : DeclarativeSemanticExtension
{
    public static readonly RustTestLibraryParser Instance = new();

    public RustTestLibraryParser()
    {
        Id = "rust-test";
        Name = "Rust Test Frameworks";
        Type = SemanticExtensionType.Library;
        LibraryRole = LibraryRole.TestFramework;
        IsBuiltIn = true;
        SupportedPatterns =
        [
            "criterion", "divan",
            "proptest", "quickcheck",
            "rstest", "test-case", "insta", "mockall", "wiremock", "serial_test", "tokio-test"
        ];
    }

    public override IEnumerable<PackageDescriptor> GetPackageDescriptors()
    {
        yield return new PackageDescriptor("criterion", "Criterion / Divan", LibraryRole.TestFramework, "library", ["criterion", "divan"]);
        yield return new PackageDescriptor("proptest", "Proptest / Quickcheck", LibraryRole.TestFramework, "library", ["proptest", "quickcheck"]);
        yield return new PackageDescriptor("rstest", "Rust Test Utilities", LibraryRole.TestFramework, "library", ["rstest", "test-case", "insta", "mockall", "wiremock", "serial_test", "tokio-test"]);
    }
}

