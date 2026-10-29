using System.Text.Json.Serialization;

namespace CodePassport.Engine.Models;

/// <summary>
/// Provenance of a value passed into a capability sink (e.g., URL passed to fetch/http.request,
/// command passed to child_process.exec, or path passed to fs.readFile).
/// Distinguishes legitimate caller-driven SDKs (like axios) from autonomous/trojanized sinks.
/// </summary>
public enum SinkInputProvenance
{
    Unknown = 0,
    CallerParameter = 1,
    InternalConstant = 2,
    DecodedConstant = 3,
    SensitiveSourceTaint = 4
}

/// <summary>
/// Concrete source location and semantic AST provenance for a detected capability.
/// </summary>
public record CapabilityEvidence(
    [property: JsonPropertyName("capability")] string Capability,
    [property: JsonPropertyName("dimension")] string Dimension,
    [property: JsonPropertyName("file")] string FilePath,
    [property: JsonPropertyName("line")] int Line,
    [property: JsonPropertyName("symbol")] string? SymbolName = null,
    [property: JsonPropertyName("target")] string? TargetExpression = null,
    [property: JsonPropertyName("isMinified")] bool IsMinified = false,
    [property: JsonPropertyName("provenance")] SinkInputProvenance Provenance = SinkInputProvenance.Unknown
);

/// <summary>
/// Tracks how a public function parameter flows into an internal capability sink.
/// </summary>
public record ParameterSinkBinding(
    [property: JsonPropertyName("parameterName")] string ParameterName,
    [property: JsonPropertyName("parameterIndex")] int ParameterIndex,
    [property: JsonPropertyName("sinkCapability")] string SinkCapability,
    [property: JsonPropertyName("sinkTarget")] string SinkTarget
);

/// <summary>
/// Tracks a resolved dataflow path from a sensitive source (ENV/FS/DOM/CRYPTO) into a privileged sink (NET/PROC/CODE/DOM).
/// </summary>
public record SemanticTaintFlow(
    [property: JsonPropertyName("sourceCapability")] string SourceCapability,
    [property: JsonPropertyName("sourceExpression")] string SourceExpression,
    [property: JsonPropertyName("sinkCapability")] string SinkCapability,
    [property: JsonPropertyName("sinkExpression")] string SinkExpression,
    [property: JsonPropertyName("file")] string FilePath,
    [property: JsonPropertyName("line")] int Line,
    [property: JsonPropertyName("enclosingSymbol")] string EnclosingSymbol
);

/// <summary>
/// Terminal root origin reached when tracing a dangerous call's argument upward along the backward slice.
/// </summary>
public record ArgumentRootOrigin(
    [property: JsonPropertyName("provenance")] SinkInputProvenance Provenance,
    [property: JsonPropertyName("expression")] string Expression,
    [property: JsonPropertyName("originFile")] string OriginFile,
    [property: JsonPropertyName("originLine")] int OriginLine,
    [property: JsonPropertyName("exportFunction")] string? ExportFunction = null,
    [property: JsonPropertyName("parameterName")] string? ParameterName = null,
    [property: JsonPropertyName("parameterIndex")] int? ParameterIndex = null,
    [property: JsonPropertyName("sourceCapability")] string? SourceCapability = null,
    [property: JsonPropertyName("callChain")] IReadOnlyList<string>? CallChain = null
);

/// <summary>
/// Backward slice trace for a specific argument of a dangerous sink call.
/// </summary>
public record SinkArgumentSlice(
    [property: JsonPropertyName("argumentIndex")] int ArgumentIndex,
    [property: JsonPropertyName("argumentExpression")] string ArgumentExpression,
    [property: JsonPropertyName("isLegitimateCallerDriven")] bool IsLegitimateCallerDriven,
    [property: JsonPropertyName("origins")] IReadOnlyList<ArgumentRootOrigin> Origins
);

/// <summary>
/// Entry in the package's "Dangerous Calls Map" with upward argument provenance verification.
/// </summary>
public record DangerousCallTrace(
    [property: JsonPropertyName("sinkTarget")] string SinkTarget,
    [property: JsonPropertyName("capability")] string Capability,
    [property: JsonPropertyName("file")] string FilePath,
    [property: JsonPropertyName("line")] int Line,
    [property: JsonPropertyName("enclosingFunction")] string EnclosingFunction,
    [property: JsonPropertyName("isLegitimate")] bool IsLegitimate,
    [property: JsonPropertyName("illegitimacyReason")] string? IllegitimacyReason,
    [property: JsonPropertyName("arguments")] IReadOnlyList<SinkArgumentSlice> Arguments
);

/// <summary>
/// Public exported symbol contract with its transitive capability bitmask, parameter-to-sink bindings,
/// and autonomous (non-caller-parameterized) capabilities.
/// </summary>
public record ExportedSymbolContract(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("file")] string FilePath,
    [property: JsonPropertyName("line")] int Line,
    [property: JsonPropertyName("capabilityMask")] ulong CapabilityMask,
    [property: JsonPropertyName("capabilities")] IReadOnlyList<string> Capabilities,
    [property: JsonPropertyName("autonomousCapabilityMask")] ulong AutonomousCapabilityMask = 0UL,
    [property: JsonPropertyName("autonomousCapabilities")] IReadOnlyList<string>? AutonomousCapabilities = null,
    [property: JsonPropertyName("parameterFlows")] IReadOnlyList<ParameterSinkBinding>? ParameterFlows = null
);

/// <summary>
/// Compound high-risk threat signature ("Toxic Combination") detected in a package.
/// </summary>
public record ToxicCombinationFinding(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("archetype")] string Archetype,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("matchedCapabilities")] IReadOnlyList<string> MatchedCapabilities
);

/// <summary>
/// Immutable Behavioral Passport (PackagePassport.json) for a specific package release.
/// </summary>
public record PackagePassport(
    [property: JsonPropertyName("ecosystem")] string Ecosystem,
    [property: JsonPropertyName("name")] string PackageName,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("tier")] BehavioralTier Tier,
    [property: JsonPropertyName("verdict")] RiskVerdict Verdict,
    [property: JsonPropertyName("capabilityMask")] ulong CapabilityMask,
    [property: JsonPropertyName("activeCapabilities")] IReadOnlyList<string> ActiveCapabilities,
    [property: JsonPropertyName("activeDimensions")] IReadOnlyList<string> ActiveDimensions,
    [property: JsonPropertyName("installHooks")] IReadOnlyDictionary<string, string> InstallHooks,
    [property: JsonPropertyName("moduleInitCapabilities")] IReadOnlyList<string> ModuleInitCapabilities,
    [property: JsonPropertyName("dormantCapabilities")] IReadOnlyList<string> DormantCapabilities,
    [property: JsonPropertyName("exports")] IReadOnlyDictionary<string, ExportedSymbolContract> Exports,
    [property: JsonPropertyName("toxicCombinations")] IReadOnlyList<ToxicCombinationFinding> ToxicCombinations,
    [property: JsonPropertyName("evidence")] IReadOnlyList<CapabilityEvidence> Evidence,
    [property: JsonPropertyName("generatedAtUtc")] DateTimeOffset GeneratedAtUtc,
    [property: JsonPropertyName("taintFlows")] IReadOnlyList<SemanticTaintFlow>? TaintFlows = null,
    [property: JsonPropertyName("autonomousTargetLiterals")] IReadOnlyList<string>? AutonomousTargetLiterals = null,
    [property: JsonPropertyName("dangerousCalls")] IReadOnlyList<DangerousCallTrace>? DangerousCalls = null
);

/// <summary>
/// Differential behavioral drift between version v(N-1) and version v(N).
/// </summary>
public record PassportDiffResult(
    [property: JsonPropertyName("ecosystem")] string Ecosystem,
    [property: JsonPropertyName("name")] string PackageName,
    [property: JsonPropertyName("fromVersion")] string FromVersion,
    [property: JsonPropertyName("toVersion")] string ToVersion,
    [property: JsonPropertyName("fromTier")] BehavioralTier FromTier,
    [property: JsonPropertyName("toTier")] BehavioralTier ToTier,
    [property: JsonPropertyName("verdict")] RiskVerdict Verdict,
    [property: JsonPropertyName("addedCapabilityMask")] ulong AddedCapabilityMask,
    [property: JsonPropertyName("removedCapabilityMask")] ulong RemovedCapabilityMask,
    [property: JsonPropertyName("addedCapabilities")] IReadOnlyList<string> AddedCapabilities,
    [property: JsonPropertyName("removedCapabilities")] IReadOnlyList<string> RemovedCapabilities,
    [property: JsonPropertyName("newInstallHooks")] IReadOnlyList<string> NewInstallHooks,
    [property: JsonPropertyName("newToxicCombinations")] IReadOnlyList<ToxicCombinationFinding> NewToxicCombinations,
    [property: JsonPropertyName("newEvidence")] IReadOnlyList<CapabilityEvidence> NewEvidence,
    [property: JsonPropertyName("newTaintFlows")] IReadOnlyList<SemanticTaintFlow>? NewTaintFlows = null,
    [property: JsonPropertyName("newAutonomousTargets")] IReadOnlyList<string>? NewAutonomousTargets = null,
    [property: JsonPropertyName("newModuleInitCapabilities")] IReadOnlyList<string>? NewModuleInitCapabilities = null,
    [property: JsonPropertyName("newIllegitimateCalls")] IReadOnlyList<DangerousCallTrace>? NewIllegitimateCalls = null
);
