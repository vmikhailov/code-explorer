using System.Text.RegularExpressions;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser.Layers;

internal static class SemanticTopologySynthesizer
{
    internal readonly record struct PeerMeshEvidence(
        bool PeerConfiguration,
        bool BidirectionalTransport,
        bool ComposeReplicas,
        bool CollectiveCompute,
        int? MemberCount,
        IReadOnlyList<string> Protocols)
    {
        public bool IsMesh => new[]
        {
            PeerConfiguration,
            BidirectionalTransport,
            ComposeReplicas,
            CollectiveCompute
        }.Count(hasEvidence => hasEvidence) >= 2;
    }

    private sealed record CloudResourceDefinition(
        string IdSuffix,
        string Name,
        string ResourceType,
        string Role,
        Regex FieldPattern,
        Regex ReadPattern,
        Regex WritePattern,
        string PackageName);

    private static readonly CloudResourceDefinition[] AwsResources =
    [
        new(
            "aws:s3:encrypted-shares",
            "AWS S3: Encrypted Shares Bucket",
            "S3Bucket",
            "encrypted_shares",
            new Regex(@"\b(?:encrypted_)?shares?_bucket(?:_name)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\b(?:get_object|head_object|list_objects(?:_v2)?)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\b(?:put_object|upload_part|complete_multipart_upload)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "aws-sdk-s3"),
        new(
            "aws:s3:database-chunks",
            "AWS S3: Database Chunks & WAL Bucket",
            "S3Bucket",
            "database_chunks",
            new Regex(@"\b(?:(?:db|database)_)?(?:chunks?|wal|parquet)_bucket(?:_name)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\b(?:get_object|head_object|list_objects(?:_v2)?)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\b(?:put_object|upload_part|complete_multipart_upload)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "aws-sdk-s3"),
        new(
            "aws:kms:share-encryption-keys",
            "AWS KMS: Share Encryption Keys",
            "KmsKeySet",
            "share_encryption_keys",
            new Regex(@"\b(?:kms_.*key|key_.*kms|kms_key_arns?)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\b(?:decrypt|describe_key|get_public_key)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\b(?:encrypt|generate_data_key|create_key)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "aws-sdk-kms")
    ];

    private static readonly Regex PeerConfigurationPattern = new(
        @"\b(?:party_id|node_id|peer_id|node_hostnames|service_ports|service_outbound_ports)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PeerListenerPattern = new(
        @"(?:TcpListener::bind|Server::builder|axum::serve)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PeerConnectorPattern = new(
        @"(?:TcpStream::connect|Endpoint::connect|Channel::from_shared|connect_lazy)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ComposeParticipantPattern = new(
        @"\b(?<name>[A-Za-z0-9-]+_participant_\d+)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    internal static IReadOnlyList<string> DetectAwsResourceRoles(
        string source,
        IReadOnlySet<string> packageNames) =>
        AwsResources
            .Where(resource => packageNames.Contains(resource.PackageName) && resource.FieldPattern.IsMatch(source))
            .Select(resource => resource.Role)
            .ToArray();

    internal static PeerMeshEvidence EvaluatePeerMeshEvidence(
        string rustSource,
        string composeSource,
        bool collectiveCompute)
    {
        var participantIds = ComposeParticipantPattern.Matches(composeSource)
            .Select(match => match.Groups["name"].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var peerConfiguration = PeerConfigurationPattern.IsMatch(rustSource);
        var bidirectionalTransport = PeerListenerPattern.IsMatch(rustSource) && PeerConnectorPattern.IsMatch(rustSource);
        var composeReplicas = participantIds.Length > 1;
        var protocols = new List<string>();
        if (rustSource.Contains("axum::serve", StringComparison.Ordinal) &&
            rustSource.Contains("coordination", StringComparison.OrdinalIgnoreCase))
        {
            protocols.Add("HTTP coordination");
        }
        if (bidirectionalTransport)
        {
            protocols.Add("TCP/mTLS");
        }
        if (rustSource.Contains("tonic", StringComparison.OrdinalIgnoreCase))
        {
            protocols.Add("gRPC");
        }
        if (collectiveCompute)
        {
            protocols.Add("NCCL");
        }

        return new PeerMeshEvidence(
            peerConfiguration,
            bidirectionalTransport,
            composeReplicas,
            collectiveCompute,
            composeReplicas ? participantIds.Length : null,
            protocols);
    }

    public static void Synthesize(
        IReadOnlyList<ProjectNode> projects,
        IReadOnlyDictionary<string, List<FileNode>> filesByProjectId,
        IReadOnlyList<Relationship> projectDependencies,
        IReadOnlyList<PackageNode> packages,
        IReadOnlyDictionary<string, string> workloadByProjectId,
        IReadOnlyList<FileNode> workspaceFiles,
        SemanticStructureNode semanticStructureNode,
        List<Relationship> semanticRelationships,
        HashSet<(string From, string To, string Kind)> existingRelKeys,
        ParsingContext ctx)
    {
        var packageById = packages
            .GroupBy(package => package.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var projectSource = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var projectPackageNames = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var project in projects.Where(project =>
                     project.ProjectType.Equals("rust", StringComparison.OrdinalIgnoreCase) && !project.IsTest))
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();
            var sources = filesByProjectId.GetValueOrDefault(project.Id) ?? [];
            var source = string.Join('\n', sources
                .Where(file => file.Name.EndsWith(".rs", StringComparison.OrdinalIgnoreCase) &&
                              !WorkspaceConventions.IsTestFilePath(file.Path))
                .Select(file => File.ReadAllText(file.FullPath)));
            projectSource[project.Id] = source;

            var packageNames = projectDependencies
                .Where(dependency => dependency.From.Equals(project.Id, StringComparison.OrdinalIgnoreCase) &&
                                     packageById.ContainsKey(dependency.To))
                .Select(dependency => packageById[dependency.To].Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            projectPackageNames[project.Id] = packageNames;

            SynthesizeAwsResources(
                project,
                source,
                packageNames,
                workloadByProjectId,
                semanticStructureNode,
                semanticRelationships,
                existingRelKeys,
                ctx);
        }

        SynthesizePeerMesh(
            projects,
            projectSource,
            projectPackageNames,
            workloadByProjectId,
            workspaceFiles,
            semanticStructureNode,
            semanticRelationships,
            existingRelKeys,
            ctx);
    }

    private static void SynthesizeAwsResources(
        ProjectNode project,
        string source,
        HashSet<string> packageNames,
        IReadOnlyDictionary<string, string> workloadByProjectId,
        SemanticStructureNode semanticStructureNode,
        List<Relationship> semanticRelationships,
        HashSet<(string From, string To, string Kind)> existingRelKeys,
        ParsingContext ctx)
    {
        var detectedRoles = DetectAwsResourceRoles(source, packageNames);
        foreach (var resource in AwsResources.Where(resource => detectedRoles.Contains(resource.Role, StringComparer.OrdinalIgnoreCase)))
        {
            var resourceId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.CloudService}:{resource.IdSuffix}";
            if (!semanticStructureNode.Children.Any(child => child.Id.Equals(resourceId, StringComparison.OrdinalIgnoreCase)))
            {
                var extensions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["provider"] = "AWS",
                    ["resource_type"] = resource.ResourceType,
                    ["resource_role"] = resource.Role,
                    ["is_semantic_entity"] = "true"
                };
                semanticStructureNode.Children.Add(new CloudServiceNode(
                    resourceId,
                    resource.Name,
                    resource.ResourceType,
                    project.Path,
                    extensions));
                ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.CloudService, resource.Name, resourceId);
            }

            var sourceId = workloadByProjectId.GetValueOrDefault(project.Id, project.Id);
            AddRelationship(
                new Relationship(sourceId, resourceId, OntologyConstants.Relationships.UsesCloud,
                    new Dictionary<string, object>
                    {
                        ["category"] = "external_resource",
                        ["dependency_type"] = "cloud_resource",
                        ["resource_role"] = resource.Role,
                        ["is_semantic"] = "true"
                    }),
                semanticRelationships,
                existingRelKeys);

            if (resource.ReadPattern.IsMatch(source))
            {
                AddRelationship(
                    new Relationship(sourceId, resourceId, OntologyConstants.Relationships.ReadsFrom,
                        new Dictionary<string, object>
                        {
                            ["category"] = "external_resource",
                            ["dependency_type"] = "cloud_resource",
                            ["resource_role"] = resource.Role,
                            ["is_semantic"] = "true"
                        }),
                    semanticRelationships,
                    existingRelKeys);
            }

            if (resource.WritePattern.IsMatch(source))
            {
                AddRelationship(
                    new Relationship(sourceId, resourceId, OntologyConstants.Relationships.WritesTo,
                        new Dictionary<string, object>
                        {
                            ["category"] = "external_resource",
                            ["dependency_type"] = "cloud_resource",
                            ["resource_role"] = resource.Role,
                            ["is_semantic"] = "true"
                        }),
                    semanticRelationships,
                    existingRelKeys);
            }
        }
    }

    private static void SynthesizePeerMesh(
        IReadOnlyList<ProjectNode> projects,
        IReadOnlyDictionary<string, string> projectSource,
        IReadOnlyDictionary<string, HashSet<string>> projectPackageNames,
        IReadOnlyDictionary<string, string> workloadByProjectId,
        IReadOnlyList<FileNode> workspaceFiles,
        SemanticStructureNode semanticStructureNode,
        List<Relationship> semanticRelationships,
        HashSet<(string From, string To, string Kind)> existingRelKeys,
        ParsingContext ctx)
    {
        var rustSource = string.Join('\n', projectSource.Values);
        var composeFiles = workspaceFiles
            .Where(file => IsComposeFile(file.Path) && !file.Name.Contains(".test.", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var composeSource = string.Join('\n', composeFiles.Select(file => File.ReadAllText(file.FullPath)));
        var collectiveCompute = projects.Any(project =>
            projectSource.TryGetValue(project.Id, out var source) &&
            source.Contains("nccl", StringComparison.OrdinalIgnoreCase) &&
            projectPackageNames.GetValueOrDefault(project.Id)?.Contains("cudarc") == true);
        var evidence = EvaluatePeerMeshEvidence(rustSource, composeSource, collectiveCompute);
        if (!evidence.IsMesh)
        {
            return;
        }

        var protocols = evidence.Protocols;
        var memberCount = evidence.MemberCount;
        var meshName = memberCount is > 1 ? $"{memberCount}-Party Peer Mesh" : "Peer Mesh";
        var meshId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.PeerMesh}:peer-network";
        if (!semanticStructureNode.Children.Any(child => child.Id.Equals(meshId, StringComparison.OrdinalIgnoreCase)))
        {
            var extensions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["topology"] = "symmetric",
                ["detection_signals"] = string.Join(',', new[]
                {
                    evidence.PeerConfiguration ? "peer configuration" : null,
                    evidence.BidirectionalTransport ? "bidirectional transport" : null,
                    evidence.ComposeReplicas ? "compose replicas" : null,
                    evidence.CollectiveCompute ? "collective compute" : null
                }.Where(signal => signal != null)),
                ["is_semantic_entity"] = "true"
            };
            semanticStructureNode.Children.Add(new PeerMeshNode(
                meshId,
                meshName,
                ".",
                memberCount,
                string.Join(", ", protocols),
                extensions));
            ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.PeerMesh, meshName, meshId);
        }

        var participantNames = ComposeParticipantPattern.Matches(composeSource)
            .Select(match => match.Groups["name"].Value)
            .Select(name => name[..name.LastIndexOf("_participant_", StringComparison.OrdinalIgnoreCase)])
            .Select(NormalizeName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var peerProjectIds = projects
            .Where(project =>
            {
                if (project.IsTest ||
                    !projectSource.TryGetValue(project.Id, out var source) ||
                    !workloadByProjectId.TryGetValue(project.Id, out var workloadId) ||
                    semanticStructureNode.Children.FirstOrDefault(node =>
                        node.Id.Equals(workloadId, StringComparison.OrdinalIgnoreCase)) is not (ServiceNode or WorkerNode or AppNode))
                {
                    return false;
                }

                var peerConfiguration = PeerConfigurationPattern.IsMatch(source);
                var bidirectionalTransport = PeerListenerPattern.IsMatch(source) && PeerConnectorPattern.IsMatch(source);
                var collective = source.Contains("nccl", StringComparison.OrdinalIgnoreCase) &&
                                projectPackageNames.GetValueOrDefault(project.Id)?.Contains("cudarc") == true;
                var independentCodeSignals = new[] { peerConfiguration, bidirectionalTransport, collective }
                    .Count(hasSignal => hasSignal);
                var normalizedProjectName = NormalizeName(project.Name);
                var composeParticipant = participantNames.Any(name =>
                    name.Length >= 3 &&
                    (name.Contains(normalizedProjectName, StringComparison.OrdinalIgnoreCase) ||
                     normalizedProjectName.Contains(name, StringComparison.OrdinalIgnoreCase)));
                return independentCodeSignals >= 2 || (independentCodeSignals >= 1 && composeParticipant);
            })
            .Select(project => project.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var project in projects.Where(project =>
                     project.ProjectType.Equals("rust", StringComparison.OrdinalIgnoreCase) && !project.IsTest))
        {
            if (!peerProjectIds.Contains(project.Id))
            {
                continue;
            }

            var sourceId = workloadByProjectId.GetValueOrDefault(project.Id, project.Id);
            var relationship = new CoordinatesWithRelationship(
                sourceId,
                meshId,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["protocols"] = string.Join(", ", protocols),
                    ["is_semantic"] = "true"
                });
            AddRelationship(
                Relationship.FromRelationship(relationship),
                semanticRelationships,
                existingRelKeys);
        }
    }

    private static bool IsComposeFile(string path)
    {
        var fileName = Path.GetFileName(path);
        var extension = Path.GetExtension(fileName);
        return (extension.Equals(".yml", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase)) &&
               (fileName.StartsWith("docker-compose", StringComparison.OrdinalIgnoreCase) ||
                fileName.StartsWith("compose", StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeName(string value) =>
        string.Concat(value.Where(char.IsLetterOrDigit));

    private static void AddRelationship(
        Relationship relationship,
        List<Relationship> semanticRelationships,
        HashSet<(string From, string To, string Kind)> existingRelKeys)
    {
        if (existingRelKeys.Add((relationship.From, relationship.To, relationship.Kind)))
        {
            semanticRelationships.Add(relationship);
        }
    }
}
