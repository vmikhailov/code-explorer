import json
import os
import subprocess
import sys
import time

sys.stdout.reconfigure(encoding="utf-8")

CE_EXE = r"c:\Work\Personal\code-explorer\cli\.Build\bin_Release_AnyCPU\CodeExplorer\ce.exe"
CBM_EXE = r"C:\Work\bench-pilot\tools\cbm\codebase-memory-mcp.exe"
CBM_CACHE = os.path.expanduser(r"~\.cache\cbm-bench-pilot")

REPOS = {
    "immich": {
        "path": r"C:\Work\bench-pilot\repos\immich",
        "cbm_cache": CBM_CACHE,
        "cbm_project": "C-Work-bench-pilot-repos-immich",
        "outline_file": "server/src/services/auth.service.ts",
    },
    "twenty": {
        "path": r"C:\Work\bench-pilot\repos\twenty",
        "cbm_cache": CBM_CACHE,
        "cbm_project": "C-Work-bench-pilot-repos-twenty",
        "outline_file": "packages/twenty-server/src/engine/core-modules/auth/auth.resolver.ts",
    },
}


class McpStdioClient:
    def __init__(self, cmd, cwd=None, env=None):
        merged_env = os.environ.copy()
        if env:
            merged_env.update(env)
        self.proc = subprocess.Popen(
            cmd,
            cwd=cwd,
            env=merged_env,
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.DEVNULL,
            text=True,
            encoding="utf-8",
            bufsize=1,
        )
        self.msg_id = 0

    def rpc(self, method, params=None):
        self.msg_id += 1
        req = {"jsonrpc": "2.0", "id": self.msg_id, "method": method}
        if params is not None:
            req["params"] = params
        line = json.dumps(req) + "\n"
        t0 = time.perf_counter()
        self.proc.stdin.write(line)
        self.proc.stdin.flush()
        while True:
            resp_line = self.proc.stdout.readline()
            if not resp_line:
                raise RuntimeError(f"EOF waiting for {method}")
            resp_line = resp_line.strip()
            if not resp_line:
                continue
            try:
                data = json.loads(resp_line)
                if data.get("id") == self.msg_id:
                    elapsed_ms = (time.perf_counter() - t0) * 1000.0
                    return data, elapsed_ms, len(resp_line.encode("utf-8"))
            except json.JSONDecodeError:
                continue

    def initialize(self):
        self.rpc(
            "initialize",
            {
                "protocolVersion": "2024-11-05",
                "capabilities": {},
                "clientInfo": {"name": "bench-client", "version": "1.0.0"},
            },
        )
        self.proc.stdin.write(
            json.dumps({"jsonrpc": "2.0", "method": "notifications/initialized"}) + "\n"
        )
        self.proc.stdin.flush()

    def call_tool(self, name, arguments):
        return self.rpc("tools/call", {"name": name, "arguments": arguments})

    def close(self):
        try:
            self.proc.terminate()
            self.proc.wait(timeout=3)
        except Exception:
            self.proc.kill()


def ensure_taxonomy_persisted(repo_path):
    """Run one call so legacy v1.72.1 DBs populate metadata['taxonomy_json'] as ce scan v1.72.2+ does."""
    client = McpStdioClient([CE_EXE, "mcp", "--root", repo_path], cwd=repo_path)
    try:
        client.initialize()
        _, backfill_ms, size_b = client.call_tool("get_taxonomy", {})
        return round(backfill_ms, 1), size_b
    finally:
        client.close()


def benchmark_repo(repo_name, cfg):
    repo_path = cfg["path"]
    backfill_ms, _ = ensure_taxonomy_persisted(repo_path)
    print(f"[{repo_name}] Taxonomy metadata backfill/verify took {backfill_ms} ms")

    # Fresh MCP stdio sessions (testing cold process with metadata['taxonomy_json'] populated)
    ce = McpStdioClient([CE_EXE, "mcp", "--root", repo_path], cwd=repo_path)
    cbm = McpStdioClient(
        [CBM_EXE],
        cwd=repo_path,
        env={"CBM_CACHE_DIR": cfg["cbm_cache"]},
    )
    try:
        ce.initialize()
        cbm.initialize()

        ce_tools_resp, _, _ = ce.rpc("tools/list", {})
        cbm_tools_resp, _, _ = cbm.rpc("tools/list", {})
        ce_tools = [t["name"] for t in ce_tools_resp["result"]["tools"]]
        cbm_tools = [t["name"] for t in cbm_tools_resp["result"]["tools"]]

        proj = cfg["cbm_project"]
        outline_file = cfg["outline_file"]

        results = {
            "repo": repo_name,
            "ce_tool_count": len(ce_tools),
            "cbm_tool_count": len(cbm_tools),
            "taxonomy_backfill_ms": backfill_ms,
            "operations": {},
        }

        ops = [
            (
                "architecture_overview",
                ("get_architecture_map", {}),
                ("get_architecture", {"project": proj}),
            ),
            (
                "graph_schema_first_call",
                ("get_taxonomy", {}),
                ("get_graph_schema", {"project": proj}),
            ),
            (
                "graph_schema_cached_call",
                ("get_taxonomy", {}),
                ("get_graph_schema", {"project": proj}),
            ),
            (
                "symbol_search_AuthService",
                ("find_symbol", {"name": "AuthService"}),
                ("search_graph", {"project": proj, "name_pattern": "AuthService"}),
            ),
            (
                "inbound_callers_validate",
                ("get_callers", {"target": "validate"}),
                ("trace_path", {"project": proj, "function_name": "validate", "direction": "inbound"}),
            ),
            (
                "file_outline",
                ("get_file_outline", {"filePath": outline_file}),
                ("get_file_outline", {"project": proj, "file_path": outline_file}),
            ),
            (
                "cypher_query_limit_10",
                ("execute_custom_read_cypher", {"query": "MATCH (n:Function) RETURN n.name LIMIT 10"}),
                ("query_graph", {"project": proj, "query": "MATCH (n:Method) RETURN n.name LIMIT 10"}),
            ),
            (
                "data_lineage_user",
                ("inspect_data_lineage", {"tableName": "user"}),
                None,
            ),
            (
                "project_dependencies",
                ("get_project_dependencies", {}),
                None,
            ),
            (
                "semantic_gates",
                ("check_semantic_gates", {}),
                None,
            ),
        ]

        for op_name, ce_call, cbm_call in ops:
            entry = {}
            if ce_call:
                _, ms, sz = ce.call_tool(ce_call[0], ce_call[1])
                entry["ce"] = {"tool": ce_call[0], "ms": round(ms, 1), "kb": round(sz / 1024.0, 1)}
            if cbm_call:
                _, ms, sz = cbm.call_tool(cbm_call[0], cbm_call[1])
                entry["cbm"] = {"tool": cbm_call[0], "ms": round(ms, 1), "kb": round(sz / 1024.0, 1)}
            results["operations"][op_name] = entry
            print(f"  {op_name}: {entry}")

        return results
    finally:
        ce.close()
        cbm.close()


def main():
    all_res = {}
    for repo_name, cfg in REPOS.items():
        all_res[repo_name] = benchmark_repo(repo_name, cfg)

    out_path = os.path.join(
        os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
        "results",
        "mcp-benchmark.json",
    )
    with open(out_path, "w", encoding="utf-8") as f:
        json.dump(all_res, f, indent=2)
    print(f"\nSaved MCP benchmark telemetry to {out_path}")


if __name__ == "__main__":
    main()
