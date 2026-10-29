using CodePassport.Engine.Models;

namespace CodePassport.Engine.Classification;

/// <summary>
/// Canonical registry of the 10-Dimensional Supply-Chain Capability Taxonomy,
/// Behavioral Tiers (Tier 0–3), and Toxic Combination compound threat signatures.
/// </summary>
public static class CapabilityCatalog
{
    public sealed record CapabilityDescriptor(
        CapabilityFlags Flag,
        string CanonicalId,
        CapabilityDimension Dimension,
        string DimensionCode,
        string Description
    );

    public sealed record ToxicCombinationRule(
        string Id,
        string Archetype,
        string Description,
        Func<CapabilityFlags, bool> Predicate,
        CapabilityFlags RelevantMask
    );

    private static readonly IReadOnlyList<CapabilityDescriptor> AllDescriptors =
    [
        // 1. NET
        new(CapabilityFlags.NetHttpClient,        "NET.HTTP_CLIENT",         CapabilityDimension.Net,    "NET",    "Outbound HTTP/HTTPS client calls"),
        new(CapabilityFlags.NetServer,            "NET.SERVER",              CapabilityDimension.Net,    "NET",    "Listening server or bound network socket"),
        new(CapabilityFlags.NetWebSocket,         "NET.WS",                  CapabilityDimension.Net,    "NET",    "Bidirectional WebSocket or raw TCP/TLS stream"),
        new(CapabilityFlags.NetRawIp,             "NET.RAW_IP",              CapabilityDimension.Net,    "NET",    "Hardcoded IPv4/IPv6 literal endpoint"),
        new(CapabilityFlags.NetExfilHost,         "NET.EXFIL_HOST",          CapabilityDimension.Net,    "NET",    "Known tunnel, webhook, paste, or C2 endpoint"),
        new(CapabilityFlags.NetDnsRaw,            "NET.DNS_RAW",             CapabilityDimension.Net,    "NET",    "Direct DNS TXT/MX/SRV resolution (DNS tunneling)"),

        // 2. FS
        new(CapabilityFlags.FsRead,               "FS.READ",                 CapabilityDimension.Fs,     "FS",     "Local file or directory read access"),
        new(CapabilityFlags.FsWrite,              "FS.WRITE",                CapabilityDimension.Fs,     "FS",     "File creation, modification, or chmod"),
        new(CapabilityFlags.FsDelete,             "FS.DELETE",               CapabilityDimension.Fs,     "FS",     "File or directory deletion / wipe"),
        new(CapabilityFlags.FsSensitivePath,      "FS.SENSITIVE_PATH",       CapabilityDimension.Fs,     "FS",     "Access to credentials, SSH/cloud keys, or wallets"),
        new(CapabilityFlags.FsTraversal,          "FS.TRAVERSAL",            CapabilityDimension.Fs,     "FS",     "Parent directory traversal outside package root"),

        // 3. PROC
        new(CapabilityFlags.ProcSpawn,            "PROC.SPAWN",              CapabilityDimension.Proc,   "PROC",   "Subprocess or shell command execution"),
        new(CapabilityFlags.ProcNative,           "PROC.NATIVE",             CapabilityDimension.Proc,   "PROC",   "Native binary addon, FFI, or ctypes binding"),
        new(CapabilityFlags.ProcShellDropper,     "PROC.SHELL_DROPPER",      CapabilityDimension.Proc,   "PROC",   "Inline shell pipeline downloader/dropper"),
        new(CapabilityFlags.ProcDaemon,           "PROC.DAEMON",             CapabilityDimension.Proc,   "PROC",   "Detached background process execution"),

        // 4. ENV
        new(CapabilityFlags.EnvConfig,            "ENV.CONFIG",              CapabilityDimension.Env,    "ENV",    "Standard benign runtime configuration read"),
        new(CapabilityFlags.EnvSecretRead,        "ENV.SECRET_READ",         CapabilityDimension.Env,    "ENV",    "Targeted read of tokens, keys, or secrets from env"),
        new(CapabilityFlags.EnvDumpAll,           "ENV.DUMP_ALL",            CapabilityDimension.Env,    "ENV",    "Wholesale serialization or iteration of process.env"),
        new(CapabilityFlags.EnvFingerprint,       "ENV.FINGERPRINT",         CapabilityDimension.Env,    "ENV",    "Host/user/OS/network interface fingerprinting"),
        new(CapabilityFlags.EnvCiEvasion,         "ENV.CI_EVASION",          CapabilityDimension.Env,    "ENV",    "Conditional branching on CI/sandbox environment variables"),

        // 5. CODE
        new(CapabilityFlags.CodeReflect,          "CODE.REFLECT",            CapabilityDimension.Code,   "CODE",   "Dynamic reflection or prototype inspection"),
        new(CapabilityFlags.CodeEval,             "CODE.EVAL",               CapabilityDimension.Code,   "CODE",   "Runtime string-to-code compilation (eval / new Function)"),
        new(CapabilityFlags.CodeVm,               "CODE.VM",                 CapabilityDimension.Code,   "CODE",   "In-process VM context or WebAssembly instantiation"),
        new(CapabilityFlags.CodeComputedImport,   "CODE.COMPUTED_IMPORT",    CapabilityDimension.Code,   "CODE",   "Non-literal dynamic require/import expression"),
        new(CapabilityFlags.CodeMonkeyPatch,      "CODE.MONKEY_PATCH",       CapabilityDimension.Code,   "CODE",   "Mutation of global builtins or module loader cache"),
        new(CapabilityFlags.CodeTimebomb,         "CODE.TIMEBOMB",           CapabilityDimension.Code,   "CODE",   "Delayed timer or date-gated execution"),

        // 6. CRYPTO
        new(CapabilityFlags.CryptoHash,           "CRYPTO.HASH",             CapabilityDimension.Crypto, "CRYPTO", "Standard cryptographic hashing or HMAC"),
        new(CapabilityFlags.CryptoCipher,         "CRYPTO.CIPHER",           CapabilityDimension.Crypto, "CRYPTO", "Symmetric/asymmetric encryption or key derivation"),
        new(CapabilityFlags.CryptoObfuscated,     "CRYPTO.OBFUSCATED_PAYLOAD", CapabilityDimension.Crypto, "CRYPTO", "High-entropy encoded blob or multi-stage decode chain"),

        // 7. HOOK
        new(CapabilityFlags.HookBuild,            "HOOK.BUILD",              CapabilityDimension.Hook,   "HOOK",   "Standard compile/prepare build script"),
        new(CapabilityFlags.HookInstallExec,      "HOOK.INSTALL_EXEC",       CapabilityDimension.Hook,   "HOOK",   "Automatic install-time script execution (pre/postinstall)"),

        // 8. DIST
        new(CapabilityFlags.DistBundled,          "DIST.BUNDLED",            CapabilityDimension.Dist,   "DIST",   "Minified or webpack-bundled distribution artifact"),
        new(CapabilityFlags.DistShadowCapability, "DIST.SHADOW_CAPABILITY",  CapabilityDimension.Dist,   "DIST",   "Capability present in dist/minified code but absent in src"),
        new(CapabilityFlags.DistBinaryBlob,       "DIST.BINARY_BLOB",        CapabilityDimension.Dist,   "DIST",   "Bundled executable binary or opaque payload"),

        // 9. DOM
        new(CapabilityFlags.DomUi,                "DOM.UI",                  CapabilityDimension.Dom,    "DOM",    "Standard DOM element rendering and event listeners"),
        new(CapabilityFlags.DomStorage,           "DOM.STORAGE",             CapabilityDimension.Dom,    "DOM",    "Access to document.cookie, localStorage, or IndexedDB"),
        new(CapabilityFlags.DomWeb3,              "DOM.WEB3",                CapabilityDimension.Dom,    "DOM",    "Interaction with Web3 wallets (window.ethereum / solana)"),
        new(CapabilityFlags.DomInject,            "DOM.INJECT",              CapabilityDimension.Dom,    "DOM",    "Dynamic script/iframe injection into DOM"),
        new(CapabilityFlags.DomCapture,           "DOM.CAPTURE",             CapabilityDimension.Dom,    "DOM",    "Global form/input keylogging or clipboard interception"),
        new(CapabilityFlags.DomBeacon,            "DOM.BEACON",              CapabilityDimension.Dom,    "DOM",    "Silent cross-origin exfiltration via sendBeacon/Image"),

        // 10. DEPS
        new(CapabilityFlags.DepsDeclared,         "DEPS.DECLARED",           CapabilityDimension.Deps,   "DEPS",   "Standard declared package dependencies"),
        new(CapabilityFlags.DepsEscalation,       "DEPS.ESCALATION",         CapabilityDimension.Deps,   "DEPS",   "Transitive dependency introduces higher privilege tier"),
        new(CapabilityFlags.DepsExternalTarball,  "DEPS.EXTERNAL_TARBALL",   CapabilityDimension.Deps,   "DEPS",   "Dependency resolved from raw URL or git commit")
    ];

    private static readonly IReadOnlyList<ToxicCombinationRule> ToxicRules =
    [
        new(
            Id: "TOXIC-001",
            Archetype: "Credential Harvester",
            Description: "Reads sensitive environment secrets or dumps process.env alongside outbound network egress.",
            Predicate: mask =>
                HasAny(mask, CapabilityFlags.EnvSecretRead | CapabilityFlags.EnvDumpAll) &&
                HasAny(mask, CapabilityFlags.NetHttpClient | CapabilityFlags.NetWebSocket | CapabilityFlags.NetDnsRaw | CapabilityFlags.NetRawIp | CapabilityFlags.NetExfilHost),
            RelevantMask: CapabilityFlags.EnvSecretRead | CapabilityFlags.EnvDumpAll |
                          CapabilityFlags.NetHttpClient | CapabilityFlags.NetWebSocket | CapabilityFlags.NetDnsRaw | CapabilityFlags.NetRawIp | CapabilityFlags.NetExfilHost
        ),
        new(
            Id: "TOXIC-002",
            Archetype: "Steganographic Dropper",
            Description: "Combines high-entropy obfuscated payloads with runtime code evaluation, VM execution, or subprocess spawning.",
            Predicate: mask =>
                HasAny(mask, CapabilityFlags.CryptoObfuscated) &&
                HasAny(mask, CapabilityFlags.CodeEval | CapabilityFlags.CodeVm | CapabilityFlags.ProcSpawn | CapabilityFlags.ProcShellDropper),
            RelevantMask: CapabilityFlags.CryptoObfuscated | CapabilityFlags.CodeEval | CapabilityFlags.CodeVm | CapabilityFlags.ProcSpawn | CapabilityFlags.ProcShellDropper
        ),
        new(
            Id: "TOXIC-003",
            Archetype: "Install-Time Trojan",
            Description: "Executes automatic install-time hooks combined with network egress, subprocess execution, or sensitive file access.",
            Predicate: mask =>
                HasAny(mask, CapabilityFlags.HookInstallExec) &&
                HasAny(mask, CapabilityFlags.NetHttpClient | CapabilityFlags.NetWebSocket | CapabilityFlags.NetRawIp | CapabilityFlags.NetExfilHost |
                             CapabilityFlags.ProcSpawn | CapabilityFlags.ProcShellDropper | CapabilityFlags.FsSensitivePath),
            RelevantMask: CapabilityFlags.HookInstallExec | CapabilityFlags.NetHttpClient | CapabilityFlags.NetWebSocket | CapabilityFlags.NetRawIp |
                          CapabilityFlags.NetExfilHost | CapabilityFlags.ProcSpawn | CapabilityFlags.ProcShellDropper | CapabilityFlags.FsSensitivePath
        ),
        new(
            Id: "TOXIC-004",
            Archetype: "Key Exfiltrator",
            Description: "Reads sensitive host credential paths (~/.ssh, ~/.aws, ~/.kube, .env) alongside network egress.",
            Predicate: mask =>
                HasAny(mask, CapabilityFlags.FsSensitivePath) &&
                HasAny(mask, CapabilityFlags.NetHttpClient | CapabilityFlags.NetWebSocket | CapabilityFlags.NetRawIp | CapabilityFlags.NetExfilHost | CapabilityFlags.NetDnsRaw),
            RelevantMask: CapabilityFlags.FsSensitivePath | CapabilityFlags.NetHttpClient | CapabilityFlags.NetWebSocket | CapabilityFlags.NetRawIp | CapabilityFlags.NetExfilHost | CapabilityFlags.NetDnsRaw
        ),
        new(
            Id: "TOXIC-005",
            Archetype: "Web3 / Session Drainer",
            Description: "Accesses browser session storage, input capture, or Web3 wallet providers alongside external network or beacon exfiltration.",
            Predicate: mask =>
                HasAny(mask, CapabilityFlags.DomWeb3 | CapabilityFlags.DomStorage | CapabilityFlags.DomCapture) &&
                HasAny(mask, CapabilityFlags.DomBeacon | CapabilityFlags.DomInject | CapabilityFlags.NetRawIp | CapabilityFlags.NetExfilHost | CapabilityFlags.NetHttpClient | CapabilityFlags.NetWebSocket),
            RelevantMask: CapabilityFlags.DomWeb3 | CapabilityFlags.DomStorage | CapabilityFlags.DomCapture |
                          CapabilityFlags.DomBeacon | CapabilityFlags.DomInject | CapabilityFlags.NetRawIp | CapabilityFlags.NetExfilHost | CapabilityFlags.NetHttpClient | CapabilityFlags.NetWebSocket
        ),
        new(
            Id: "TOXIC-006",
            Archetype: "Build Tampering (Shadow Capability)",
            Description: "Distribution or minified bundle exhibits privileged capabilities that do not exist in the unminified source tree.",
            Predicate: mask => HasAny(mask, CapabilityFlags.DistShadowCapability),
            RelevantMask: CapabilityFlags.DistShadowCapability
        )
    ];

    public static IReadOnlyList<CapabilityDescriptor> Descriptors => AllDescriptors;

    public static IReadOnlyList<string> FormatCapabilities(CapabilityFlags mask)
    {
        if (mask == CapabilityFlags.None) return [];
        var result = new List<string>();
        foreach (var desc in AllDescriptors)
        {
            if ((mask & desc.Flag) != 0)
            {
                result.Add(desc.CanonicalId);
            }
        }
        return result;
    }

    public static IReadOnlyList<string> FormatDimensions(CapabilityFlags mask)
    {
        if (mask == CapabilityFlags.None) return [];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>();
        foreach (var desc in AllDescriptors)
        {
            if ((mask & desc.Flag) != 0 && seen.Add(desc.DimensionCode))
            {
                result.Add(desc.DimensionCode);
            }
        }
        return result;
    }

    public static string GetDimensionCode(CapabilityFlags singleFlag)
    {
        foreach (var desc in AllDescriptors)
        {
            if (desc.Flag == singleFlag) return desc.DimensionCode;
        }
        return "UNKNOWN";
    }

    public static string GetCanonicalId(CapabilityFlags singleFlag)
    {
        foreach (var desc in AllDescriptors)
        {
            if (desc.Flag == singleFlag) return desc.CanonicalId;
        }
        return singleFlag.ToString();
    }

    /// <summary>
    /// Classifies a package into its Behavioral Tier (Tier 0–3) based on its active capability bitmask.
    /// </summary>
    public static BehavioralTier ClassifyTier(CapabilityFlags mask)
    {
        const CapabilityFlags tier3Flags =
            CapabilityFlags.ProcSpawn |
            CapabilityFlags.ProcNative |
            CapabilityFlags.ProcShellDropper |
            CapabilityFlags.ProcDaemon |
            CapabilityFlags.FsWrite |
            CapabilityFlags.FsDelete |
            CapabilityFlags.FsSensitivePath |
            CapabilityFlags.FsTraversal |
            CapabilityFlags.HookInstallExec |
            CapabilityFlags.CodeEval |
            CapabilityFlags.CodeVm |
            CapabilityFlags.CodeMonkeyPatch |
            CapabilityFlags.DistShadowCapability |
            CapabilityFlags.DistBinaryBlob;

        if ((mask & tier3Flags) != 0)
        {
            return BehavioralTier.Tier3SystemPrivileged;
        }

        const CapabilityFlags tier2Flags =
            CapabilityFlags.NetHttpClient |
            CapabilityFlags.NetServer |
            CapabilityFlags.NetWebSocket |
            CapabilityFlags.NetRawIp |
            CapabilityFlags.NetExfilHost |
            CapabilityFlags.NetDnsRaw |
            CapabilityFlags.DomWeb3 |
            CapabilityFlags.DomBeacon |
            CapabilityFlags.DomInject;

        if ((mask & tier2Flags) != 0)
        {
            return BehavioralTier.Tier2NetworkClient;
        }

        const CapabilityFlags tier1Flags =
            CapabilityFlags.EnvConfig |
            CapabilityFlags.EnvSecretRead |
            CapabilityFlags.EnvDumpAll |
            CapabilityFlags.EnvFingerprint |
            CapabilityFlags.EnvCiEvasion |
            CapabilityFlags.FsRead |
            CapabilityFlags.DomUi |
            CapabilityFlags.DomStorage |
            CapabilityFlags.DomCapture |
            CapabilityFlags.CryptoHash |
            CapabilityFlags.CryptoCipher |
            CapabilityFlags.CryptoObfuscated |
            CapabilityFlags.CodeReflect |
            CapabilityFlags.CodeComputedImport |
            CapabilityFlags.CodeTimebomb |
            CapabilityFlags.HookBuild |
            CapabilityFlags.DistBundled;

        if ((mask & tier1Flags) != 0)
        {
            return BehavioralTier.Tier1LocalPassive;
        }

        return BehavioralTier.Tier0Pure;
    }

    /// <summary>
    /// Evaluates a capability bitmask against the 6 high-risk Toxic Combination signatures.
    /// </summary>
    public static IReadOnlyList<ToxicCombinationFinding> EvaluateToxicCombinations(CapabilityFlags mask)
    {
        if (mask == CapabilityFlags.None) return [];
        var findings = new List<ToxicCombinationFinding>();
        foreach (var rule in ToxicRules)
        {
            if (rule.Predicate(mask))
            {
                var matched = FormatCapabilities(mask & rule.RelevantMask);
                findings.Add(new ToxicCombinationFinding(
                    rule.Id,
                    rule.Archetype,
                    rule.Description,
                    matched));
            }
        }
        return findings;
    }

    private static bool HasAny(CapabilityFlags mask, CapabilityFlags flags) => (mask & flags) != 0;
}
