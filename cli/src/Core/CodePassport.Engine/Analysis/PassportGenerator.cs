using System.Threading.Channels;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Layers;
using CodePassport.Engine.Classification;
using CodePassport.Engine.Models;
using CodePassport.Engine.Semantics;

namespace CodePassport.Engine.Analysis;

/// <summary>
/// Generates an immutable <see cref="PackagePassport"/> by executing CodeExplorer's canonical
/// Layer 1 -> Layer 5 ontology pipeline (<see cref="Layer1PhysicalParser"/>, <see cref="Layer2ProjectParser"/>,
/// <see cref="Layer3SyntacticParser"/>, Layer 4 <see cref="ApiInUseNode"/> enrichment, and
/// Layer 5 <see cref="BackwardProvenanceSlicer"/>) over a package directory.
/// </summary>
public sealed class PassportGenerator
{
    private static readonly string[] PassportExcludedDirectories =
    [
        ".git",
        ".codeexplorer",
        "node_modules",
        "bin",
        "obj",
        "target",
        ".venv",
        "venv",
        "__pycache__",
        "test",
        "tests",
        "__tests__",
        "spec",
        "example",
        "examples",
        "benchmark",
        "benchmarks",
        "docs"
    ];

    public async Task<PackagePassport> GenerateAsync(string packageDirectoryPath, CancellationToken ct = default)
    {
        WorkspaceIndexer.AutoRegisterParsers();

        var fullDir = Path.GetFullPath(packageDirectoryPath);
        if (!Directory.Exists(fullDir))
        {
            throw new DirectoryNotFoundException($"Package directory not found: '{fullDir}'");
        }

        await using var dbClient = new InMemoryGraphClient();
        var channel = Channel.CreateUnbounded<Func<Task>>();
        var channelTask = Task.Run(async () =>
        {
            await foreach (var work in channel.Reader.ReadAllAsync(ct))
            {
                await work();
            }
        }, ct);

        var ctx = new ParsingContext(
            absoluteWorkspacePath: fullDir,
            hostWorkspacePath: fullDir,
            dbClient: dbClient,
            sharedChannel: channel,
            clear: true,
            cancellationToken: ct)
        {
            FileFilter = new WorkspaceFileFilter(
                WorkspaceIndexer.GetAllProjectParsers(),
                WorkspaceIndexer.GetAllFileParsers(),
                PassportExcludedDirectories)
            {
                IncludeDistAndMinifiedArtifacts = true
            }
        };

        Layer3Result? l3Result = null;
        try
        {
            // Layer 1: Physical Topology & ArtifactRole classification ("source", "dist", "minified", "script")
            var l1Parser = new Layer1PhysicalParser();
            var l1Result = await l1Parser.ParseAsync(ctx);

            // Layer 2: Project Boundaries, Produced/Consumed PackageNodes, and EntryPointNodes (lifecycle-hook, build-hook, package-export)
            var l2Parser = new Layer2ProjectParser();
            var l2Result = await l2Parser.ParseAsync(l1Result, ctx);

            var (ecosystem, packageName, version, manifestMask, installHooks, manifestEvidence) =
                ExtractLayer2ManifestMetadata(l2Result, fullDir);

            // Layer 3: Syntactic AST, Universal SyntaxDocument, SyntaxValueEvaluator (constant/decode folding), and Function/Parameter/Prototype nodes
            var l3Parser = new Layer3SyntacticParser();
            l3Result = await l3Parser.ParseAsync(l2Result, ctx);
            var triggeredFileIds = l2Result.ProjectDependencies
                .Where(r => r.Kind == OntologyConstants.Relationships.Triggers || r.Kind == OntologyConstants.Relationships.DeclaredIn)
                .Select(r => r.To)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var parsedTrees = l3Result.SyntaxTrees
                .Where(t => !ShouldSkipAuxiliaryPackageFile(t, triggeredFileIds))
                .ToList();

            // Layer 4: Semantic Capability & Taint Analysis (emitting Layer 4 ApiInUseNode onto SemanticStructureNode)
            var semanticStructureId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.SemanticStructure}";
            var semanticStructureNode = new SemanticStructureNode(semanticStructureId, "SemanticStructure", l1Result.Workspace.Path);
            l1Result.Workspace.Children.Add(semanticStructureNode);
            ctx.SemanticStructure = semanticStructureNode;

            var crossModuleExports = new Dictionary<string, IReadOnlyDictionary<string, SymbolicBinding>>(StringComparer.OrdinalIgnoreCase);
            foreach (var tree in parsedTrees)
            {
                var relPath = tree.RelativePath.Replace('\\', '/');
                var initialResult = CapabilityScanner.ScanSyntaxTree(tree, crossModuleExports: null);
                crossModuleExports[relPath] = initialResult.ExportedBindings;
            }

            var srcMask = CapabilityFlags.None;
            var distMask = CapabilityFlags.None;
            var moduleInitMask = manifestMask;
            var hasSrcFiles = false;
            var hasDistFiles = false;

            var allEvidence = new List<CapabilityEvidence>(manifestEvidence);
            var allTaintFlows = new List<SemanticTaintFlow>();
            var allAutonomousTargets = new HashSet<string>(StringComparer.Ordinal);
            var allExports = new Dictionary<string, ExportedSymbolContract>(StringComparer.Ordinal);

            foreach (var tree in parsedTrees)
            {
                var relPath = tree.RelativePath.Replace('\\', '/');
                var fileResult = CapabilityScanner.ScanSyntaxTree(tree, crossModuleExports);
                crossModuleExports[relPath] = fileResult.ExportedBindings;

                moduleInitMask |= fileResult.ModuleInitMask;
                allEvidence.AddRange(fileResult.Evidence);
                allTaintFlows.AddRange(fileResult.TaintFlows);
                foreach (var target in fileResult.AutonomousTargetLiterals)
                {
                    allAutonomousTargets.Add(target);
                }

                // Emit Layer 4 ApiInUseNodes for discovered capability evidence
                foreach (var ev in fileResult.Evidence)
                {
                    var apiName = !string.IsNullOrEmpty(ev.TargetExpression) ? ev.TargetExpression : ev.Capability;
                    var apiId = $"{ctx.WorkspaceId}:p::api:{ev.FilePath}:{ev.Line}:{ev.Capability}";
                    var role = ev.Dimension is "NET" or "PROC" or "CODE" ? "Sink" : ev.Dimension == "ENV" ? "SensitiveSource" : "Capability";
                    var apiNode = new ApiInUseNode(
                        Id: apiId,
                        Name: apiName,
                        Path: ev.FilePath,
                        Capability: ev.Capability,
                        Dimension: ev.Dimension,
                        SemanticRole: role);
                    semanticStructureNode.Children.Add(apiNode);
                }

                var isDistOrMinified = tree.FileNode.ArtifactRole is "dist" or "minified" || fileResult.IsMinifiedOrDist;
                if (isDistOrMinified)
                {
                    hasDistFiles = true;
                    distMask |= fileResult.TotalMask;
                }
                else
                {
                    hasSrcFiles = true;
                    srcMask |= fileResult.TotalMask;
                }

                foreach (var (exportName, contract) in fileResult.Exports)
                {
                    var key = allExports.ContainsKey(exportName) ? $"{relPath}:{exportName}" : exportName;
                    allExports[key] = contract;
                }
            }

            var totalMask = manifestMask | srcMask | distMask;

            // Layer 5: Global Provenance Slicing & Passport Verdict Synthesis
            var backwardSlicer = new BackwardProvenanceSlicer();
            var dangerousCalls = backwardSlicer.BuildDangerousCallMapAndVerify(parsedTrees, crossModuleExports);
            var hasIllegitimateCalls = dangerousCalls.Any(dc => !dc.IsLegitimate);

            // Detect DIST.SHADOW_CAPABILITY: privileged capabilities in dist/minified files that are absent in src/
            // (Only flag when dist/ introduces illegitimate/autonomous sink calls, taint flows, or secret reads)
            if (hasSrcFiles && hasDistFiles)
            {
                const CapabilityFlags privilegedShadowMask =
                    CapabilityFlags.NetHttpClient |
                    CapabilityFlags.NetWebSocket |
                    CapabilityFlags.NetRawIp |
                    CapabilityFlags.NetExfilHost |
                    CapabilityFlags.NetDnsRaw |
                    CapabilityFlags.ProcSpawn |
                    CapabilityFlags.ProcNative |
                    CapabilityFlags.ProcShellDropper |
                    CapabilityFlags.FsSensitivePath |
                    CapabilityFlags.EnvSecretRead |
                    CapabilityFlags.EnvDumpAll |
                    CapabilityFlags.CodeEval |
                    CapabilityFlags.CodeMonkeyPatch |
                    CapabilityFlags.DomWeb3 |
                    CapabilityFlags.DomBeacon;

                var shadowDiff = (distMask & ~srcMask) & privilegedShadowMask;
                if (shadowDiff != CapabilityFlags.None &&
                    (hasIllegitimateCalls || allTaintFlows.Count > 0 || (shadowDiff & CapabilityFlags.EnvSecretRead) != 0))
                {
                    totalMask |= CapabilityFlags.DistShadowCapability;
                    moduleInitMask |= CapabilityFlags.DistShadowCapability;
                    var shadowCaps = string.Join(", ", CapabilityCatalog.FormatCapabilities(shadowDiff));
                    allEvidence.Add(new CapabilityEvidence(
                        CapabilityCatalog.GetCanonicalId(CapabilityFlags.DistShadowCapability),
                        "DIST",
                        "dist/",
                        1,
                        "<bundle_diff>",
                        $"Capabilities in dist/ absent from src/: [{shadowCaps}]",
                        IsMinified: true,
                        Provenance: SinkInputProvenance.InternalConstant));
                }
            }

            var dormantMask = totalMask & ~moduleInitMask;
            var tier = CapabilityCatalog.ClassifyTier(totalMask);

            // Evaluate toxic combinations over autonomous/unverified capabilities:
            // if 100% of dangerous sink calls are proven legitimate caller-driven (or there are no dangerous sink calls) and there are no taint flows,
            // only evaluate manifest-level hooks/shadow capabilities rather than triggering bag-of-flags toxic combinations on module imports or caller-parameterized sinks.
            var toxicEvaluationMask = (!hasIllegitimateCalls && allTaintFlows.Count == 0)
                ? (manifestMask | (totalMask & CapabilityFlags.DistShadowCapability))
                : totalMask;
            var toxicFindings = CapabilityCatalog.EvaluateToxicCombinations(toxicEvaluationMask);

            var verdict = RiskVerdict.Safe;
            if (toxicFindings.Count > 0 ||
                allTaintFlows.Count > 0 ||
                hasIllegitimateCalls ||
                (totalMask & CapabilityFlags.DistShadowCapability) != 0)
            {
                verdict = RiskVerdict.CriticalDrift;
            }
            else if (tier >= BehavioralTier.Tier3SystemPrivileged)
            {
                verdict = RiskVerdict.Expansion;
            }

            return new PackagePassport(
                Ecosystem: ecosystem,
                PackageName: packageName,
                Version: version,
                Tier: tier,
                Verdict: verdict,
                CapabilityMask: (ulong)totalMask,
                ActiveCapabilities: CapabilityCatalog.FormatCapabilities(totalMask),
                ActiveDimensions: CapabilityCatalog.FormatDimensions(totalMask),
                InstallHooks: installHooks,
                ModuleInitCapabilities: CapabilityCatalog.FormatCapabilities(moduleInitMask),
                DormantCapabilities: CapabilityCatalog.FormatCapabilities(dormantMask),
                Exports: allExports,
                ToxicCombinations: toxicFindings,
                Evidence: allEvidence,
                GeneratedAtUtc: DateTimeOffset.UtcNow,
                TaintFlows: allTaintFlows,
                AutonomousTargetLiterals: [.. allAutonomousTargets],
                DangerousCalls: dangerousCalls);
        }
        finally
        {
            channel.Writer.TryComplete();
            try
            {
                await channelTask;
            }
            catch
            {
                // Ignore background channel drain exceptions on cancellation
            }

            if (l3Result != null)
            {
                foreach (var tree in l3Result.SyntaxTrees)
                {
                    tree.Dispose();
                }
            }
        }
    }

    private static (
        string Ecosystem,
        string PackageName,
        string Version,
        CapabilityFlags Mask,
        IReadOnlyDictionary<string, string> InstallHooks,
        IReadOnlyList<CapabilityEvidence> Evidence)
        ExtractLayer2ManifestMetadata(Layer2Result l2Result, string fullDir)
    {
        var fallbackName = Path.GetFileName(fullDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var rootProject = l2Result.Projects
            .OrderBy(p => p.Path?.Length ?? 0)
            .FirstOrDefault();

        if (rootProject == null)
        {
            return ("generic", fallbackName, "0.0.0", CapabilityFlags.None, new Dictionary<string, string>(), []);
        }

        var producedPkg = rootProject.Children
            .OfType<PackageNode>()
            .FirstOrDefault(p => p.IsInternal);

        var ecosystem = NormalizeEcosystem(producedPkg?.Ecosystem ?? rootProject.ProjectType);
        var packageName = producedPkg?.Name ?? rootProject.Name ?? fallbackName;
        var version = producedPkg?.Version ?? "0.0.0";
        var mask = CapabilityFlags.None;
        var hooks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var evidence = new List<CapabilityEvidence>();

        foreach (var project in l2Result.Projects)
        {
            foreach (var entry in project.Children.OfType<EntryPointNode>())
            {
                var cmd = entry.Extensions?.GetValueOrDefault("command") ?? string.Empty;
                if (string.Equals(entry.EntryType, "lifecycle-hook", StringComparison.OrdinalIgnoreCase))
                {
                    hooks[entry.Name] = cmd;
                    mask |= CapabilityFlags.HookInstallExec;
                    evidence.Add(new CapabilityEvidence(
                        CapabilityCatalog.GetCanonicalId(CapabilityFlags.HookInstallExec),
                        "HOOK",
                        string.IsNullOrEmpty(entry.Path) ? "package.json" : entry.Path,
                        1,
                        $"scripts.{entry.Name}",
                        cmd,
                        IsMinified: false,
                        Provenance: SinkInputProvenance.InternalConstant));

                    var cmdFlags = AstSymbolicEvaluator.ClassifyResolvedStringConstant(cmd, isDecoded: false);
                    if (cmdFlags != CapabilityFlags.None)
                    {
                        mask |= cmdFlags;
                    }
                }
                else if (string.Equals(entry.EntryType, "build-hook", StringComparison.OrdinalIgnoreCase))
                {
                    mask |= CapabilityFlags.HookBuild;
                }
            }

            foreach (var depPkg in project.Children.OfType<PackageNode>().Where(p => !p.IsInternal))
            {
                var scope = depPkg.Extensions?.GetValueOrDefault("dependency_scope") ?? "runtime";
                if (!string.Equals(scope, "runtime", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                mask |= CapabilityFlags.DepsDeclared;
                if (string.Equals(depPkg.Extensions?.GetValueOrDefault("is_external_tarball"), "true", StringComparison.OrdinalIgnoreCase))
                {
                    mask |= CapabilityFlags.DepsExternalTarball;
                    evidence.Add(new CapabilityEvidence(
                        CapabilityCatalog.GetCanonicalId(CapabilityFlags.DepsExternalTarball),
                        "DEPS",
                        "package.json",
                        1,
                        $"dependencies.{depPkg.Name}",
                        depPkg.Version,
                        IsMinified: false,
                        Provenance: SinkInputProvenance.InternalConstant));
                }
            }
        }

        return (ecosystem, packageName, version, mask, hooks, evidence);
    }

    private static string NormalizeEcosystem(string rawEcosystem) =>
        rawEcosystem.ToLowerInvariant() switch
        {
            "typescript" or "javascript" or "npm" => "npm",
            "csharp" or "dotnet" or "nuget" => "nuget",
            "python" or "pypi" => "pypi",
            "rust" or "cargo" => "cargo",
            "go" or "golang" => "go",
            var other => other
        };

    private static bool ShouldSkipAuxiliaryPackageFile(SyntaxTree tree, HashSet<string> triggeredFileIds)
    {
        if (tree.FileNode != null && triggeredFileIds.Contains(tree.FileNode.Id))
        {
            return false;
        }

        if (tree.FileNode?.IsTest == true)
        {
            return true;
        }

        var rel = tree.RelativePath.Replace('\\', '/');
        var fileName = Path.GetFileName(rel);
        if (fileName.StartsWith("test-", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".test.js", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".test.ts", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".spec.js", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".spec.ts", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".py", StringComparison.OrdinalIgnoreCase) ||
            fileName.Contains(".cov.", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("sandbox.js", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("cli.js", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("cli.mjs", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("cli.cjs", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("license-update.mjs", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith("create-", StringComparison.OrdinalIgnoreCase) ||
            rel.Equals("src/build.js", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var padded = "/" + rel.TrimStart('/');
        if (padded.Contains("/scripts/", StringComparison.OrdinalIgnoreCase) ||
            padded.Contains("/tools/", StringComparison.OrdinalIgnoreCase) ||
            padded.Contains("/bin/", StringComparison.OrdinalIgnoreCase) ||
            padded.Contains("/examples/", StringComparison.OrdinalIgnoreCase) ||
            padded.Contains("/example/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
