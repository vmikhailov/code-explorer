using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.CSharp.Libraries;

/// <summary>
/// Library parser for .NET test and benchmark frameworks (xUnit, NUnit, MSTest, TUnit, bUnit, BenchmarkDotNet, Microsoft.NET.Test.Sdk).
/// </summary>
public sealed class DotNetTestLibraryParser : ISemanticExtension
{
    public static readonly DotNetTestLibraryParser Instance = new();

    public string Id => "dotnet-test";
    public string Name => ".NET Test Frameworks";
    public string Type => "library";
    public LibraryRole LibraryRole => LibraryRole.TestFramework;

    public IReadOnlyList<string> SupportedPatterns =>
    [
        "xunit", "xunit.*", "Verify.Xunit",
        "nunit", "nunit.*", "NUnit3TestAdapter", "Verify.NUnit",
        "MSTest", "MSTest.*", "Microsoft.VisualStudio.TestPlatform*",
        "TUnit", "TUnit.*",
        "Microsoft.NET.Test.Sdk", "Microsoft.Testing.Platform*", "Microsoft.Testing.Extensions*", "Microsoft.AspNetCore.Mvc.Testing",
        "bunit", "bunit.*",
        "BenchmarkDotNet", "BenchmarkDotNet.*",
        "FluentAssertions", "Shouldly"
    ];

    public bool IsImplemented => true;
    public bool IsBuiltIn => true;

    public IEnumerable<PackageDescriptor> GetPackageDescriptors()
    {
        yield return new PackageDescriptor("xunit", "xUnit", LibraryRole.TestFramework, "library", ["xunit", "xunit.*", "Verify.Xunit"]);
        yield return new PackageDescriptor("nunit", "NUnit", LibraryRole.TestFramework, "library", ["nunit", "nunit.*", "NUnit3TestAdapter", "Verify.NUnit"]);
        yield return new PackageDescriptor("mstest", "MSTest", LibraryRole.TestFramework, "library", ["MSTest", "MSTest.*", "Microsoft.VisualStudio.TestPlatform*"]);
        yield return new PackageDescriptor("tunit", "TUnit", LibraryRole.TestFramework, "library", ["TUnit", "TUnit.*"]);
        yield return new PackageDescriptor("nettestsdk", "Microsoft.NET.Test.Sdk", LibraryRole.TestFramework, "library", ["Microsoft.NET.Test.Sdk", "Microsoft.Testing.Platform*", "Microsoft.Testing.Extensions*", "Microsoft.AspNetCore.Mvc.Testing"]);
        yield return new PackageDescriptor("bunit", "bUnit", LibraryRole.TestFramework, "library", ["bunit", "bunit.*"]);
        yield return new PackageDescriptor("benchmarkdotnet", "BenchmarkDotNet", LibraryRole.TestFramework, "library", ["BenchmarkDotNet", "BenchmarkDotNet.*"]);
        yield return new PackageDescriptor("fluentassertions", "FluentAssertions", LibraryRole.TestFramework, "library", ["FluentAssertions", "Shouldly"]);
    }

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;
}

