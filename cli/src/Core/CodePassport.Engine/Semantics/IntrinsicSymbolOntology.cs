using CodePassport.Engine.Models;

namespace CodePassport.Engine.Semantics;

public enum IntrinsicRole
{
    ModuleNamespace,
    GlobalObject,
    CapabilitySink,
    SensitiveSource,
    DecodeTransform,
    SerializeTransform,
    LocalFunctionRef
}

/// <summary>
/// Represents the resolved semantic identity of a symbol, expression, or member access in the AST.
/// </summary>
public sealed record SymbolicBinding(
    IntrinsicRole Role,
    string CanonicalPath,
    CapabilityFlags Capability = CapabilityFlags.None,
    string? ResolvedConstant = null,
    bool IsDecodedFromObfuscation = false,
    int? SourceParameterIndex = null,
    string? SourceParameterName = null,
    CapabilityFlags TaintMask = CapabilityFlags.None,
    string? TaintSourceExpr = null,
    string? TargetLocalFunctionName = null
);

/// <summary>
/// Language-agnostic semantic ontology of OS, runtime, DOM, and Web3 intrinsics.
/// Operates purely on resolved AST symbol bindings (never on raw text substrings).
/// </summary>
public static class IntrinsicSymbolOntology
{
    private static readonly Dictionary<string, (IntrinsicRole Role, CapabilityFlags Flag)> CanonicalModuleMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Node.js / JS / TS modules
        ["http"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.NetHttpClient),
        ["https"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.NetHttpClient),
        ["http2"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.NetHttpClient),
        ["undici"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.NetHttpClient),
        ["axios"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.NetHttpClient),
        ["node-fetch"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.NetHttpClient),
        ["got"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.NetHttpClient),
        ["superagent"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.NetHttpClient),
        ["net"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.NetWebSocket),
        ["tls"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.NetWebSocket),
        ["dgram"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.NetWebSocket),
        ["ws"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.NetWebSocket),
        ["dns"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.NetDnsRaw),
        ["dns/promises"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.NetDnsRaw),
        ["fs"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.FsRead),
        ["fs/promises"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.FsRead),
        ["fs-extra"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.FsRead),
        ["child_process"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.ProcSpawn),
        ["execa"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.ProcSpawn),
        ["cross-spawn"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.ProcSpawn),
        ["shelljs"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.ProcSpawn),
        ["os"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.EnvFingerprint),
        ["vm"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.CodeVm),
        ["vm2"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.CodeVm),
        ["crypto"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.CryptoCipher),
        ["ffi-napi"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.ProcNative),
        ["bindings"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.ProcNative),
        ["ethers"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.DomWeb3),
        ["web3"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.DomWeb3),
        ["@solana/web3.js"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.DomWeb3),

        // Python modules
        ["subprocess"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.ProcSpawn),
        ["ctypes"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.ProcNative),
        ["cffi"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.ProcNative),
        ["socket"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.NetWebSocket),
        ["requests"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.NetHttpClient),
        ["simple-get"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.NetHttpClient),
        ["urllib"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.NetHttpClient),
        ["urllib.request"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.NetHttpClient),
        ["httpx"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.NetHttpClient),
        ["aiohttp"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.NetHttpClient),
        ["shutil"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.FsWrite),

        // Go / Rust / .NET namespaces
        ["os/exec"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.ProcSpawn),
        ["net/http"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.NetHttpClient),
        ["syscall"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.ProcNative),
        ["System.Diagnostics"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.ProcSpawn),
        ["System.Net.Http"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.NetHttpClient),
        ["System.Reflection"] = (IntrinsicRole.ModuleNamespace, CapabilityFlags.CodeReflect)
    };

    private static readonly Dictionary<string, (IntrinsicRole Role, CapabilityFlags Flag)> CanonicalMemberRules = new(StringComparer.Ordinal)
    {
        // Global & Window roots
        ["global"] = (IntrinsicRole.GlobalObject, CapabilityFlags.None),
        ["globalThis"] = (IntrinsicRole.GlobalObject, CapabilityFlags.None),
        ["window"] = (IntrinsicRole.GlobalObject, CapabilityFlags.None),
        ["self"] = (IntrinsicRole.GlobalObject, CapabilityFlags.None),
        ["process"] = (IntrinsicRole.GlobalObject, CapabilityFlags.None),
        ["document"] = (IntrinsicRole.GlobalObject, CapabilityFlags.None),
        ["navigator"] = (IntrinsicRole.GlobalObject, CapabilityFlags.None),
        ["Buffer"] = (IntrinsicRole.GlobalObject, CapabilityFlags.None),
        ["JSON"] = (IntrinsicRole.GlobalObject, CapabilityFlags.None),
        ["Object"] = (IntrinsicRole.GlobalObject, CapabilityFlags.None),
        ["String"] = (IntrinsicRole.GlobalObject, CapabilityFlags.None),
        ["WebAssembly"] = (IntrinsicRole.GlobalObject, CapabilityFlags.CodeVm),
        ["Reflect"] = (IntrinsicRole.GlobalObject, CapabilityFlags.CodeReflect),

        // Global direct sinks
        ["eval"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.CodeEval),
        ["Function"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.CodeEval),
        ["setTimeout"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.CodeTimebomb),
        ["setInterval"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.CodeTimebomb),
        ["fetch"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.NetHttpClient),
        ["XMLHttpRequest"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.NetHttpClient),
        ["WebSocket"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.NetWebSocket),
        ["atob"] = (IntrinsicRole.DecodeTransform, CapabilityFlags.None),
        ["btoa"] = (IntrinsicRole.DecodeTransform, CapabilityFlags.None),

        // Process & Environment
        ["process.env"] = (IntrinsicRole.SensitiveSource, CapabilityFlags.EnvDumpAll),
        ["process.argv"] = (IntrinsicRole.GlobalObject, CapabilityFlags.None),
        ["process.execPath"] = (IntrinsicRole.GlobalObject, CapabilityFlags.None),
        ["process.execArgv"] = (IntrinsicRole.GlobalObject, CapabilityFlags.None),
        ["os.environ"] = (IntrinsicRole.SensitiveSource, CapabilityFlags.EnvDumpAll),
        ["Environment.GetEnvironmentVariable"] = (IntrinsicRole.SensitiveSource, CapabilityFlags.EnvConfig),
        ["Environment.GetEnvironmentVariables"] = (IntrinsicRole.SensitiveSource, CapabilityFlags.EnvDumpAll),
        ["process.binding"] = (IntrinsicRole.GlobalObject, CapabilityFlags.None),
        ["process.dlopen"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.ProcNative),

        // OS Fingerprinting
        ["os.userInfo"] = (IntrinsicRole.SensitiveSource, CapabilityFlags.EnvFingerprint),
        ["os.hostname"] = (IntrinsicRole.SensitiveSource, CapabilityFlags.EnvFingerprint),
        ["os.networkInterfaces"] = (IntrinsicRole.SensitiveSource, CapabilityFlags.EnvFingerprint),
        ["os.cpus"] = (IntrinsicRole.SensitiveSource, CapabilityFlags.EnvFingerprint),
        ["os.platform"] = (IntrinsicRole.SensitiveSource, CapabilityFlags.EnvFingerprint),
        ["os.release"] = (IntrinsicRole.SensitiveSource, CapabilityFlags.EnvFingerprint),
        ["os.homedir"] = (IntrinsicRole.SensitiveSource, CapabilityFlags.EnvFingerprint),

        // Child Process / OS Execution
        ["child_process.exec"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.ProcSpawn),
        ["child_process.execSync"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.ProcSpawn),
        ["child_process.spawn"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.ProcSpawn),
        ["child_process.spawnSync"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.ProcSpawn),
        ["child_process.execFile"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.ProcSpawn),
        ["child_process.execFileSync"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.ProcSpawn),
        ["child_process.fork"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.ProcSpawn),
        ["subprocess.Popen"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.ProcSpawn),
        ["subprocess.run"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.ProcSpawn),
        ["subprocess.call"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.ProcSpawn),
        ["subprocess.check_output"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.ProcSpawn),
        ["os.system"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.ProcSpawn),
        ["os.popen"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.ProcSpawn),
        ["exec.Command"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.ProcSpawn),
        ["Process.Start"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.ProcSpawn),

        // File System
        ["fs.readFile"] = (IntrinsicRole.SensitiveSource, CapabilityFlags.FsRead),
        ["fs.readFileSync"] = (IntrinsicRole.SensitiveSource, CapabilityFlags.FsRead),
        ["fs.readdir"] = (IntrinsicRole.SensitiveSource, CapabilityFlags.FsRead),
        ["fs.readdirSync"] = (IntrinsicRole.SensitiveSource, CapabilityFlags.FsRead),
        ["fs.createReadStream"] = (IntrinsicRole.SensitiveSource, CapabilityFlags.FsRead),
        ["fs/promises.readFile"] = (IntrinsicRole.SensitiveSource, CapabilityFlags.FsRead),
        ["fs.writeFile"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.FsWrite),
        ["fs.writeFileSync"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.FsWrite),
        ["fs.appendFile"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.FsWrite),
        ["fs.appendFileSync"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.FsWrite),
        ["fs.createWriteStream"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.FsWrite),
        ["fs.chmod"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.FsWrite),
        ["fs.chmodSync"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.FsWrite),
        ["fs/promises.writeFile"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.FsWrite),
        ["fs.unlink"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.FsDelete),
        ["fs.unlinkSync"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.FsDelete),
        ["fs.rm"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.FsDelete),
        ["fs.rmSync"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.FsDelete),
        ["fs.rmdirSync"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.FsDelete),
        ["File.ReadAllText"] = (IntrinsicRole.SensitiveSource, CapabilityFlags.FsRead),
        ["File.ReadAllBytes"] = (IntrinsicRole.SensitiveSource, CapabilityFlags.FsRead),
        ["File.WriteAllText"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.FsWrite),
        ["File.WriteAllBytes"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.FsWrite),
        ["File.Delete"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.FsDelete),

        // Network Egress & Server
        ["http.request"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.NetHttpClient),
        ["http.get"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.NetHttpClient),
        ["https.request"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.NetHttpClient),
        ["https.get"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.NetHttpClient),
        ["http.createServer"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.NetServer),
        ["https.createServer"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.NetServer),
        ["net.createServer"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.NetServer),
        ["net.connect"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.NetWebSocket),
        ["net.createConnection"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.NetWebSocket),
        ["tls.connect"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.NetWebSocket),
        ["dgram.createSocket"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.NetWebSocket),
        ["dns.resolve"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.NetDnsRaw),
        ["dns.resolveTxt"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.NetDnsRaw),
        ["dns.resolveMx"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.NetDnsRaw),
        ["dns.lookup"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.NetDnsRaw),
        ["axios"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.NetHttpClient),
        ["axios.get"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.NetHttpClient),
        ["axios.post"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.NetHttpClient),
        ["axios.put"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.NetHttpClient),
        ["axios.request"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.NetHttpClient),
        ["got"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.NetHttpClient),
        ["got.post"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.NetHttpClient),
        ["requests.get"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.NetHttpClient),
        ["requests.post"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.NetHttpClient),
        ["urllib.request.urlopen"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.NetHttpClient),

        // VM & Reflection & Monkey Patching
        ["vm.runInNewContext"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.CodeVm),
        ["vm.runInThisContext"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.CodeVm),
        ["vm.runInContext"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.CodeVm),
        ["vm.Script"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.CodeVm),
        ["WebAssembly.instantiate"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.CodeVm),
        ["WebAssembly.compile"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.CodeVm),
        ["Reflect.apply"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.CodeReflect),
        ["Reflect.construct"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.CodeReflect),
        ["require.cache"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.CodeMonkeyPatch),
        ["Module._load"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.CodeMonkeyPatch),

        // TypeScript / Babel CommonJS interop helpers (__importStar / __importDefault)
        ["__importStar"] = (IntrinsicRole.SerializeTransform, CapabilityFlags.None),
        ["__importDefault"] = (IntrinsicRole.SerializeTransform, CapabilityFlags.None),

        // Crypto & Decode
        ["crypto.createHash"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.CryptoHash),
        ["crypto.createHmac"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.CryptoHash),
        ["crypto.createCipheriv"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.CryptoCipher),
        ["crypto.createDecipheriv"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.CryptoCipher),
        ["crypto.publicEncrypt"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.CryptoCipher),
        ["crypto.privateDecrypt"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.CryptoCipher),
        ["crypto.scrypt"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.CryptoCipher),
        ["crypto.pbkdf2"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.CryptoCipher),
        ["Buffer.from"] = (IntrinsicRole.DecodeTransform, CapabilityFlags.None),
        ["String.fromCharCode"] = (IntrinsicRole.DecodeTransform, CapabilityFlags.None),
        ["JSON.stringify"] = (IntrinsicRole.SerializeTransform, CapabilityFlags.None),
        ["Object.entries"] = (IntrinsicRole.SerializeTransform, CapabilityFlags.None),
        ["Object.keys"] = (IntrinsicRole.SerializeTransform, CapabilityFlags.None),
        ["Object.values"] = (IntrinsicRole.SerializeTransform, CapabilityFlags.None),
        ["Object.assign"] = (IntrinsicRole.SerializeTransform, CapabilityFlags.None),

        // Browser DOM & Web3
        ["document.cookie"] = (IntrinsicRole.SensitiveSource, CapabilityFlags.DomStorage),
        ["localStorage"] = (IntrinsicRole.SensitiveSource, CapabilityFlags.DomStorage),
        ["localStorage.getItem"] = (IntrinsicRole.SensitiveSource, CapabilityFlags.DomStorage),
        ["sessionStorage"] = (IntrinsicRole.SensitiveSource, CapabilityFlags.DomStorage),
        ["sessionStorage.getItem"] = (IntrinsicRole.SensitiveSource, CapabilityFlags.DomStorage),
        ["indexedDB"] = (IntrinsicRole.SensitiveSource, CapabilityFlags.DomStorage),
        ["indexedDB.open"] = (IntrinsicRole.SensitiveSource, CapabilityFlags.DomStorage),
        ["window.ethereum"] = (IntrinsicRole.SensitiveSource, CapabilityFlags.DomWeb3),
        ["window.ethereum.request"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.DomWeb3),
        ["ethereum.request"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.DomWeb3),
        ["window.solana"] = (IntrinsicRole.SensitiveSource, CapabilityFlags.DomWeb3),
        ["window.solana.signTransaction"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.DomWeb3),
        ["window.solana.signAllTransactions"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.DomWeb3),
        ["navigator.sendBeacon"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.DomBeacon),
        ["navigator.clipboard"] = (IntrinsicRole.SensitiveSource, CapabilityFlags.DomCapture),
        ["navigator.clipboard.readText"] = (IntrinsicRole.SensitiveSource, CapabilityFlags.DomCapture),
        ["document.createElement"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.DomUi),
        ["document.addEventListener"] = (IntrinsicRole.CapabilitySink, CapabilityFlags.DomUi)
    };

    private static readonly HashSet<string> GlobalWrapperPrefixes = new(StringComparer.Ordinal)
    {
        "global",
        "globalThis",
        "window",
        "self"
    };

    public static string NormalizeModuleName(string modulePath)
    {
        var norm = modulePath.Trim().Trim('\'', '"', '`');
        if (norm.StartsWith("node:", StringComparison.OrdinalIgnoreCase))
        {
            norm = norm[5..];
        }
        return norm;
    }

    public static bool TryResolveModule(string modulePath, out SymbolicBinding binding)
    {
        var norm = NormalizeModuleName(modulePath);
        if (CanonicalModuleMap.TryGetValue(norm, out var entry))
        {
            binding = new SymbolicBinding(entry.Role, norm, entry.Flag);
            return true;
        }

        binding = default!;
        return false;
    }

    public static bool TryResolveIntrinsicPath(string canonicalPath, out SymbolicBinding binding)
    {
        var norm = StripGlobalPrefix(canonicalPath);
        if (CanonicalMemberRules.TryGetValue(norm, out var entry))
        {
            binding = new SymbolicBinding(entry.Role, norm, entry.Flag);
            return true;
        }

        // Only explicit sink methods in CanonicalMemberRules are treated as CapabilitySinks;
        // pure helper methods like net.isIP or net.isIPv6 are not dangerous sinks.
        binding = default!;
        return false;
    }

    private static string StripGlobalPrefix(string path)
    {
        foreach (var prefix in GlobalWrapperPrefixes)
        {
            if (path.StartsWith(prefix + ".", StringComparison.Ordinal))
            {
                var stripped = path[(prefix.Length + 1)..];
                if (!stripped.StartsWith("ethereum", StringComparison.Ordinal) &&
                    !stripped.StartsWith("solana", StringComparison.Ordinal))
                {
                    return stripped;
                }
            }
        }
        return path;
    }
}
