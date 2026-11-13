using System.Text.RegularExpressions;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Rust;

/// <summary>
/// File parser for Rust source files (.rs) in CodeExplorer.
/// </summary>
public class RustFileParser : IFileParser
{
    public string LanguageName => "rust";

    private static readonly Regex RustDeclarationLinePattern = new(
        @"^[ \t]*(?:#\[.*|(?:pub(?:\([^)]*\))?[ \t]+)?[A-Za-z_][A-Za-z0-9_]*[ \t]*(?::[^:].*|,[ \t]*(?://.*)?\r?)|(?:pub(?:\([^)]*\))?[ \t]+)?(?:async[ \t]+)?fn[ \t]+.*)$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex RustEncryptedSharesFieldRegex = new(@"\b(?:encrypted_)?shares?_bucket(?:_name)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RustDbChunksFieldRegex = new(@"\b(?:(?:db|database)_)?(?:chunks?|wal|parquet)_bucket(?:_name)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RustGraphCheckpointFieldRegex = new(@"\b(?:graph_)?checkpoint_(?:s3_client|bucket(?:_name)?)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RustKmsFieldRegex = new(@"\b(?:kms_.*key|key_.*kms|kms_key_arns?)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RustS3ReadRegex = new(@"\b(?:get_object|head_object|list_objects(?:_v2)?)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RustGraphReadRegex = new(@"\b(?:download_graph_checkpoint\w*)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RustS3WriteRegex = new(@"\b(?:put_object|upload_part|complete_multipart_upload)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RustGraphWriteRegex = new(@"\b(?:upload_graph_checkpoint\w*)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RustKmsReadRegex = new(@"\b(?:decrypt|describe_key|get_public_key)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RustKmsWriteRegex = new(@"\b(?:encrypt|generate_data_key|create_key)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RustPeerConfigRegex = new(@"\b(?:party_id|node_id|peer_id|node_hostnames|service_ports|service_outbound_ports)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RustPeerListenerRegex = new(@"(?:TcpListener::bind|Server::builder|axum::serve)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RustPeerConnectorRegex = new(@"(?:TcpStream::connect|Endpoint::connect|Channel::from_shared|connect_lazy)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RustPeerAddressingUsageRegex = new(@"\.\s*(?:node_hostnames|service_ports|service_outbound_ports|peer_addresses)\b", RegexOptions.Compiled);

    private const string TokEncryptedSharesBucket = "encrypted_shares_bucket";
    private const string TokDatabaseChunksBucket = "database_chunks_bucket";
    private const string TokGraphCheckpointBucket = "graph_checkpoint_bucket";
    private const string TokKmsKeyArns = "kms_key_arns";
    private const string TokGetObject = "get_object";
    private const string TokDownloadGraphCheckpoint = "download_graph_checkpoint";
    private const string TokPutObject = "put_object";
    private const string TokUploadGraphCheckpoint = "upload_graph_checkpoint";
    private const string TokDecrypt = "decrypt";
    private const string TokEncrypt = "encrypt";
    private const string TokPartyId = "party_id";
    private const string TokTcpListenerBind = "TcpListener::bind";
    private const string TokTcpStreamConnect = "TcpStream::connect";
    private const string TokPeerAddresses = ".peer_addresses";
    private const string TokAxumServe = "axum::serve";
    private const string TokCoordination = "coordination";
    private const string TokTonic = "tonic";
    private const string TokNccl = "nccl";

    public void EnrichFileSignals(
        CodeExplorer.Core.Common.Nodes.Layer1_Physical.FileNode fileNode,
        SyntaxTree syntaxTree,
        CodeExplorer.Core.Common.Nodes.Layer2_Boundaries.ProjectNode? project,
        string sourceText,
        ParsingContext? ctx)
    {
        if (WorkspaceConventions.IsTestFilePath(fileNode.Path))
        {
            return;
        }

        var fullSummary = BuildRustTopologySummary(sourceText);
        if (!string.IsNullOrEmpty(fullSummary))
        {
            CodeExplorer.Core.Common.Nodes.AnalysisSignalStore.Signals(fileNode).RustTopologySummary = fullSummary;
            CodeExplorer.Core.Common.Nodes.AnalysisSignalStore.Signals(syntaxTree.FileNode).RustTopologySummary = fullSummary;
            var bodyOnly = RustDeclarationLinePattern.Replace(sourceText, string.Empty);
            var bodySummary = BuildRustTopologySummary(bodyOnly);
            if (!string.IsNullOrEmpty(bodySummary))
            {
                CodeExplorer.Core.Common.Nodes.AnalysisSignalStore.Signals(fileNode).RustTopologyBodySummary = bodySummary;
                CodeExplorer.Core.Common.Nodes.AnalysisSignalStore.Signals(syntaxTree.FileNode).RustTopologyBodySummary = bodySummary;
            }
        }
    }

    private static string BuildRustTopologySummary(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var tokens = new List<string>(12);
        if (RustEncryptedSharesFieldRegex.IsMatch(text)) tokens.Add(TokEncryptedSharesBucket);
        if (RustDbChunksFieldRegex.IsMatch(text)) tokens.Add(TokDatabaseChunksBucket);
        if (RustGraphCheckpointFieldRegex.IsMatch(text)) tokens.Add(TokGraphCheckpointBucket);
        if (RustKmsFieldRegex.IsMatch(text)) tokens.Add(TokKmsKeyArns);
        if (RustS3ReadRegex.IsMatch(text)) tokens.Add(TokGetObject);
        if (RustGraphReadRegex.IsMatch(text)) tokens.Add(TokDownloadGraphCheckpoint);
        if (RustS3WriteRegex.IsMatch(text)) tokens.Add(TokPutObject);
        if (RustGraphWriteRegex.IsMatch(text)) tokens.Add(TokUploadGraphCheckpoint);
        if (RustKmsReadRegex.IsMatch(text)) tokens.Add(TokDecrypt);
        if (RustKmsWriteRegex.IsMatch(text)) tokens.Add(TokEncrypt);
        if (RustPeerConfigRegex.IsMatch(text)) tokens.Add(TokPartyId);
        if (RustPeerListenerRegex.IsMatch(text)) tokens.Add(TokTcpListenerBind);
        if (RustPeerConnectorRegex.IsMatch(text)) tokens.Add(TokTcpStreamConnect);
        if (RustPeerAddressingUsageRegex.IsMatch(text)) tokens.Add(TokPeerAddresses);
        if (text.Contains(TokAxumServe, StringComparison.Ordinal)) tokens.Add(TokAxumServe);
        if (text.Contains(TokCoordination, StringComparison.OrdinalIgnoreCase)) tokens.Add(TokCoordination);
        if (text.Contains(TokTonic, StringComparison.OrdinalIgnoreCase)) tokens.Add(TokTonic);
        if (text.Contains(TokNccl, StringComparison.OrdinalIgnoreCase)) tokens.Add(TokNccl);
        return tokens.Count == 0 ? string.Empty : string.Join(' ', tokens);
    }

    public bool UsesTreeSitter => true;

    public SyntaxDocument? ParseDocument(string sourceText, string filePath = "", string relativePath = "") =>
        RustSyntaxConverter.Instance.Parse(sourceText, LanguageName, filePath, relativePath);

    public LanguageSyntaxProfile SyntaxProfile => RustSyntaxProfile.Instance;

    internal static readonly IReadOnlyList<ISemanticExtension> DefaultExtensions =
    [
        new Libraries.AxumLibraryParser(),
        new Libraries.ActixWebLibraryParser(),
        new Libraries.RocketLibraryParser(),
        new Libraries.TonicLibraryParser(),
        new Libraries.ReqwestLibraryParser(),
        new Libraries.HyperLibraryParser(),
        new Libraries.SqlxLibraryParser(),
        new Libraries.SeaOrmLibraryParser(),
        new Libraries.DieselLibraryParser(),
        new Libraries.RdKafkaRustLibraryParser(),
        new Libraries.LapinLibraryParser(),
        new Libraries.AsyncNatsLibraryParser(),
        new Libraries.AwsSdkRustLibraryParser(),
        Libraries.RustTestLibraryParser.Instance
    ];

    internal static readonly IReadOnlyList<PackageDescriptor> DefaultPackages =
        [.. DefaultExtensions.SelectMany(e => e.GetPackageDescriptors())];

    public IReadOnlyList<ISemanticExtension> SemanticExtensions => DefaultExtensions;

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".rs", StringComparison.OrdinalIgnoreCase);
    }

    public void ExtractDeclarations(Node rootNode, Action<string, Node?, string?> registerDeclaration)
    {
        RustDeclarationExtractor.Extract(rootNode, registerDeclaration);
    }

    public BaseParserVisitor CreateVisitor(
        Node rootNode,
        List<ISemanticExtension> activeExtensions,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        SemanticExtensionRegistry extensionRegistry)
    {
        var fullPath = Path.Combine(absoluteWorkspacePath, relativePath);
        var crateCargoToml = RustProjectParser.FindCrateCargoToml(fullPath);
        CargoManifest? manifest = null;
        if (!string.IsNullOrEmpty(crateCargoToml) && File.Exists(crateCargoToml))
        {
            manifest = RustProjectParser.GetOrLoadManifest(crateCargoToml);
        }

        return new RustFileVisitor(
            rootNode,
            activeExtensions,
            relativePath,
            absoluteWorkspacePath,
            fileParser,
            extensionRegistry,
            manifest
        );
    }

    public async Task<SyntaxTree> ParseAsync(
        string filePath,
        string parentNodeId,
        string workspaceId,
        string absoluteWorkspacePath)
    {
        var relativePath = Path.GetRelativePath(absoluteWorkspacePath, filePath).Replace('\\', '/');
        return await SyntaxTree.ParseAsync(filePath, relativePath, parentNodeId, this, workspaceId, absoluteWorkspacePath);
    }

    public ImportType ResolveImportType(string importPath, string filePath, string? absoluteWorkspacePath)
    {
        var crateRootCargo = RustProjectParser.FindCrateCargoToml(filePath) ?? RustModuleResolver.FindCrateRoot(filePath);
        CargoManifest? manifest = null;
        if (!string.IsNullOrEmpty(crateRootCargo) && File.Exists(crateRootCargo))
        {
            manifest = RustProjectParser.GetOrLoadManifest(crateRootCargo);
        }

        return RustModuleResolver.ResolveUsePath(importPath, filePath, manifest);
    }
}
