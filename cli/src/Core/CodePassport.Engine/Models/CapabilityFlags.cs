namespace CodePassport.Engine.Models;

/// <summary>
/// The 10 orthogonal supply-chain capability dimensions across Server (OS syscall boundary)
/// and Client (Browser DOM / Web3 VM boundary) runtimes.
/// </summary>
public enum CapabilityDimension
{
    Net = 1,
    Fs = 2,
    Proc = 3,
    Env = 4,
    Code = 5,
    Crypto = 6,
    Hook = 7,
    Dist = 8,
    Dom = 9,
    Deps = 10
}

/// <summary>
/// 64-bit capability bitmask for O(1) method-level propagation and cross-package composition.
/// </summary>
[Flags]
public enum CapabilityFlags : ulong
{
    None = 0UL,

    // 1. NET: Network & Egress (Bits 0..5)
    NetHttpClient        = 1UL << 0,
    NetServer            = 1UL << 1,
    NetWebSocket         = 1UL << 2,
    NetRawIp             = 1UL << 3,
    NetExfilHost         = 1UL << 4,
    NetDnsRaw            = 1UL << 5,

    // 2. FS: File System (Bits 6..10)
    FsRead               = 1UL << 6,
    FsWrite              = 1UL << 7,
    FsDelete             = 1UL << 8,
    FsSensitivePath      = 1UL << 9,
    FsTraversal          = 1UL << 10,

    // 3. PROC: Process & OS Execution (Bits 11..14)
    ProcSpawn            = 1UL << 11,
    ProcNative           = 1UL << 12,
    ProcShellDropper     = 1UL << 13,
    ProcDaemon           = 1UL << 14,

    // 4. ENV: Environment & Host (Bits 15..19)
    EnvConfig            = 1UL << 15,
    EnvSecretRead        = 1UL << 16,
    EnvDumpAll           = 1UL << 17,
    EnvFingerprint       = 1UL << 18,
    EnvCiEvasion         = 1UL << 19,

    // 5. CODE: Dynamic Code & VM (Bits 20..25)
    CodeReflect          = 1UL << 20,
    CodeEval             = 1UL << 21,
    CodeVm               = 1UL << 22,
    CodeComputedImport   = 1UL << 23,
    CodeMonkeyPatch      = 1UL << 24,
    CodeTimebomb         = 1UL << 25,

    // 6. CRYPTO: Crypto & Encoding (Bits 26..28)
    CryptoHash           = 1UL << 26,
    CryptoCipher         = 1UL << 27,
    CryptoObfuscated     = 1UL << 28,

    // 7. HOOK: Lifecycle Scripts (Bits 29..30)
    HookBuild            = 1UL << 29,
    HookInstallExec      = 1UL << 30,

    // 8. DIST: Artifact Integrity (Bits 31..33)
    DistBundled          = 1UL << 31,
    DistShadowCapability = 1UL << 32,
    DistBinaryBlob       = 1UL << 33,

    // 9. DOM: Browser & Web3 Runtime (Bits 34..39)
    DomUi                = 1UL << 34,
    DomStorage           = 1UL << 35,
    DomWeb3              = 1UL << 36,
    DomInject            = 1UL << 37,
    DomCapture           = 1UL << 38,
    DomBeacon            = 1UL << 39,

    // 10. DEPS: Transitive Closure (Bits 40..42)
    DepsDeclared         = 1UL << 40,
    DepsEscalation       = 1UL << 41,
    DepsExternalTarball  = 1UL << 42
}

/// <summary>
/// Package behavioral archetype derived from its active capability vector.
/// </summary>
public enum BehavioralTier
{
    /// <summary>Tier 0: Pure Computation (strictly CPU and in-memory data structures).</summary>
    Tier0Pure = 0,

    /// <summary>Tier 1: Local Passive (benign ENV.CONFIG, local FS.READ, or DOM.UI).</summary>
    Tier1LocalPassive = 1,

    /// <summary>Tier 2: Network / Client SDK (NET.HTTP_CLIENT, NET.WS, or NET.SERVER; zero PROC/EVAL).</summary>
    Tier2NetworkClient = 2,

    /// <summary>Tier 3: System &amp; Build Tool (PROC.SPAWN, PROC.NATIVE, FS.WRITE, or HOOK.INSTALL_EXEC).</summary>
    Tier3SystemPrivileged = 3
}

/// <summary>
/// Risk verdict for a package release or version delta.
/// </summary>
public enum RiskVerdict
{
    Safe = 0,
    Expansion = 1,
    CriticalDrift = 2
}
