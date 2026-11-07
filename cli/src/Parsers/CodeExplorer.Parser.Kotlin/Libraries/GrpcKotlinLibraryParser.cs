using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Parser.Kotlin.Libraries;

public sealed class GrpcKotlinLibraryParser : DeclarativeSemanticExtension
{
    private const string GrpcServicePrefix = "GRPC:/";
    private const string GrpcCallPrefix = "grpc:";
    private const string ImplBaseMarker = "CoroutineImplBase";
    private const string StubMarker = "CoroutineStub";

    private static readonly Regex CoroutineImplBaseRegex = new(
        @"\bclass\s+(?<cls>[a-zA-Z0-9_]+)[^{:]*:\s*[a-zA-Z0-9_.]*?(?<svc>[a-zA-Z0-9_]+)CoroutineImplBase\b",
        RegexOptions.Compiled);

    private static readonly Regex CoroutineStubRegex = new(
        @"(?<svc>[a-zA-Z0-9_]+)CoroutineStub\s*\(",
        RegexOptions.Compiled);

    public GrpcKotlinLibraryParser()
    {
        Id = "grpc-kotlin";
        Name = "gRPC-Kotlin";
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns = ["io.grpc.kotlin*", "io.grpc*"];
    }

    public static void AnalyzeSource(string content, FileNode fileNode)
    {
        if (!content.Contains(ImplBaseMarker, StringComparison.Ordinal) &&
            !content.Contains(StubMarker, StringComparison.Ordinal))
        {
            return;
        }

        foreach (Match match in CoroutineImplBaseRegex.Matches(content))
        {
            var svcName = match.Groups[2].Value.Trim();
            if (!string.IsNullOrEmpty(svcName))
            {
                fileNode.References.Add(new Reference(fileNode.Id, GrpcServicePrefix + svcName, OntologyConstants.Relationships.Exposes));
            }
        }

        foreach (Match match in CoroutineStubRegex.Matches(content))
        {
            var svcName = match.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(svcName))
            {
                fileNode.References.Add(new Reference(fileNode.Id, GrpcCallPrefix + svcName, OntologyConstants.Relationships.Calls));
            }
        }
    }
}
