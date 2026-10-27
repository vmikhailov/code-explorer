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
            "aws:s3:graph-checkpoints",
            "AWS S3: HNSW Graph Checkpoint Bucket",
            "S3Bucket",
            "graph_checkpoints",
            new Regex(@"\b(?:graph_)?checkpoint_(?:s3_client|bucket(?:_name)?)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\b(?:get_object|head_object|list_objects(?:_v2)?|download_graph_checkpoint\w*)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\b(?:put_object|upload_part|complete_multipart_upload|upload_graph_checkpoint\w*)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
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

    private static readonly Regex PeerAddressingUsagePattern = new(
        @"\.\s*(?:node_hostnames|service_ports|service_outbound_ports|peer_addresses)\b",
        RegexOptions.Compiled);

    private static readonly Regex ComposeParticipantPattern = new(
        @"\b(?<name>[A-Za-z0-9-]+_participant_\d+)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Lines that only declare names (struct fields, field initializers/destructuring, attributes, fn signatures).
    // Shared config crates declare every resource field; declaring a field is not evidence that a workload uses it.
    private static readonly Regex DeclarationLinePattern = new(
        @"^[ \t]*(?:#\[.*|(?:pub(?:\([^)]*\))?[ \t]+)?[A-Za-z_][A-Za-z0-9_]*[ \t]*(?::[^:].*|,[ \t]*(?://.*)?\r?)|(?:pub(?:\([^)]*\))?[ \t]+)?(?:async[ \t]+)?fn[ \t]+.*)$",
        RegexOptions.Multiline | RegexOptions.Compiled);

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
        var nonTestRustProjects = projects
            .Where(project => project.ProjectType.Equals("rust", StringComparison.OrdinalIgnoreCase) && !project.IsTest)
            .ToList();
        if (nonTestRustProjects.Count == 0)
        {
            return;
        }

        var packageById = packages
            .GroupBy(package => package.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var projectById = projects
            .GroupBy(project => project.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var projectSource = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var projectEffectiveSource = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var projectPeerAddressingFiles = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var projectPackageNames = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var sourceCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        string ReadSource(string fullPath)
        {
            if (!sourceCache.TryGetValue(fullPath, out var text))
            {
                text = File.Exists(fullPath) ? File.ReadAllText(fullPath) : string.Empty;
                sourceCache[fullPath] = text;
            }
            return text;
        }

        var directPackageNames = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var localDependencyIds = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var dependency in projectDependencies)
        {
            if (packageById.TryGetValue(dependency.To, out var package))
            {
                if (!directPackageNames.TryGetValue(dependency.From, out var names))
                {
                    names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    directPackageNames[dependency.From] = names;
                }
                names.Add(package.Name);
            }
            else if (projectById.ContainsKey(dependency.To))
            {
                if (!localDependencyIds.TryGetValue(dependency.From, out var targets))
                {
                    targets = [];
                    localDependencyIds[dependency.From] = targets;
                }
                targets.Add(dependency.To);
            }
        }

        foreach (var project in nonTestRustProjects)
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();
            var sources = filesByProjectId.GetValueOrDefault(project.Id) ?? [];
            var ownFiles = sources
                .Where(file => file.Name.EndsWith(".rs", StringComparison.OrdinalIgnoreCase) &&
                              !WorkspaceConventions.IsTestFilePath(file.Path))
                .Select(file => file.FullPath)
                .ToList();
            var source = string.Join('\n', ownFiles.Select(ReadSource));
            projectSource[project.Id] = source;

            // Only long-running workloads inherit evidence from reachable dependency code; for CLIs the import-level
            // reachability is too coarse (importing a type pulls in its whole module) to attribute infrastructure.
            var isRuntimeWorkload = workloadByProjectId.TryGetValue(project.Id, out var workloadId) &&
                                    semanticStructureNode.Children.Any(node =>
                                        node.Id.Equals(workloadId, StringComparison.OrdinalIgnoreCase) &&
                                        node is ServiceNode or WorkerNode or AppNode);
            var reachableFiles = isRuntimeWorkload
                ? GetReachableDependencyFiles(project, ctx.AbsoluteWorkspacePath)
                : [];
            var reachableSources = string.Join('\n', reachableFiles
                .Select(file => DeclarationLinePattern.Replace(ReadSource(file), string.Empty)));
            var effectiveSource = reachableSources.Length == 0 ? source : source + "\n" + reachableSources;
            projectEffectiveSource[project.Id] = effectiveSource;
            projectPeerAddressingFiles[project.Id] = ownFiles.Concat(reachableFiles)
                .Where(file => PeerAddressingUsagePattern.IsMatch(ReadSource(file)))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Packages reachable through local workspace dependencies count too: a crate that only calls
            // `common::kms_dh` still talks to KMS through the dependency's aws-sdk-kms client.
            var packageNames = CollectEffectivePackageNames(project.Id, directPackageNames, localDependencyIds);
            projectPackageNames[project.Id] = packageNames;

            SynthesizeAwsResources(
                project,
                effectiveSource,
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
            projectEffectiveSource,
            projectPeerAddressingFiles,
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

            if (!workloadByProjectId.TryGetValue(project.Id, out var sourceId))
            {
                continue;
            }
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
        IReadOnlyDictionary<string, string> projectEffectiveSource,
        IReadOnlyDictionary<string, HashSet<string>> projectPeerAddressingFiles,
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
        var candidateProjectIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var peerProjectIds = projects
            .Where(project =>
            {
                if (project.IsTest ||
                    !projectEffectiveSource.TryGetValue(project.Id, out var source) ||
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
                if (independentCodeSignals >= 1)
                {
                    candidateProjectIds.Add(project.Id);
                }
                var normalizedProjectName = NormalizeName(project.Name);
                var composeParticipant = participantNames.Any(name =>
                    name.Length >= 3 &&
                    (name.Contains(normalizedProjectName, StringComparison.OrdinalIgnoreCase) ||
                     normalizedProjectName.Contains(name, StringComparison.OrdinalIgnoreCase)));
                return independentCodeSignals >= 2 || (independentCodeSignals >= 1 && composeParticipant);
            })
            .Select(project => project.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        peerProjectIds.UnionWith(FindSharedNetworkingPeers(peerProjectIds, candidateProjectIds, projectPeerAddressingFiles));

        foreach (var project in projects.Where(project =>
                     project.ProjectType.Equals("rust", StringComparison.OrdinalIgnoreCase) && !project.IsTest))
        {
            if (!peerProjectIds.Contains(project.Id) ||
                !workloadByProjectId.TryGetValue(project.Id, out var sourceId))
            {
                continue;
            }

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

    private static IReadOnlyList<string> GetReachableDependencyFiles(ProjectNode project, string workspaceRoot)
    {
        var json = project.Extensions?.GetValueOrDefault(OntologyConstants.PropertyNames.SourceReachability);
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        Dictionary<string, string[]>? reachability;
        try
        {
            reachability = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string[]>>(json);
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }

        return reachability is null
            ? []
            : reachability.Values
                .SelectMany(files => files)
                .Where(file => !WorkspaceConventions.IsTestFilePath(file))
                .Select(file => Path.GetFullPath(Path.Combine(workspaceRoot, file)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
    }

    /// <summary>
    /// A candidate workload that reaches every peer-addressing file of a confirmed mesh member runs the same
    /// peer-networking code path (e.g. alternative server binaries over one server crate), so it is a member too.
    /// Reaching only part of a member's footprint (typically just shared configuration) is not enough.
    /// </summary>
    internal static IReadOnlySet<string> FindSharedNetworkingPeers(
        IReadOnlySet<string> confirmedMemberIds,
        IReadOnlySet<string> candidateIds,
        IReadOnlyDictionary<string, HashSet<string>> peerAddressingFilesByProject)
    {
        var memberFootprints = confirmedMemberIds
            .Select(id => peerAddressingFilesByProject.GetValueOrDefault(id))
            .OfType<HashSet<string>>()
            .Where(files => files.Count > 0)
            .ToList();

        return candidateIds
            .Where(id => !confirmedMemberIds.Contains(id) &&
                         peerAddressingFilesByProject.TryGetValue(id, out var files) &&
                         memberFootprints.Any(footprint => footprint.IsSubsetOf(files)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
    private static HashSet<string> CollectEffectivePackageNames(
        string projectId,
        IReadOnlyDictionary<string, HashSet<string>> directPackageNames,
        IReadOnlyDictionary<string, List<string>> localDependencyIds)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        queue.Enqueue(projectId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current))
            {
                continue;
            }

            if (directPackageNames.TryGetValue(current, out var names))
            {
                result.UnionWith(names);
            }

            foreach (var dependencyId in localDependencyIds.GetValueOrDefault(current) ?? [])
            {
                queue.Enqueue(dependencyId);
            }
        }

        return result;
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
