import json
import os
import subprocess
import sys
import time
import psutil

sys.stdout.reconfigure(encoding="utf-8")

CE_EXE = r"c:\Work\Personal\code-explorer\cli\.Build\bin_Release_AnyCPU\CodeExplorer\ce.exe"

REPOS = {
    "immich": {
        "path": r"C:\Work\bench-pilot\repos\immich",
        "graph_json": r"C:\Work\bench-pilot\repos\immich\graphify-out\graph.json",
        "outline_file": "server/src/services/auth.service.ts",
        "path_src": "login",
        "path_tgt": "validate",
        "gfy_auth_id": "server_src_services_auth_service_authservice",
        "gfy_val_id": "server_src_services_auth_service_authservice_validate",
        "gfy_login_id": "server_src_controllers_auth_controller_authcontroller_login",
    },
    "twenty": {
        "path": r"C:\Work\bench-pilot\repos\twenty",
        "graph_json": r"C:\Work\bench-pilot\repos\twenty\graphify-out\graph.json",
        "outline_file": "packages/twenty-server/src/engine/core-modules/auth/auth.resolver.ts",
        "path_src": "login",
        "path_tgt": "validate",
        "gfy_auth_id": "authservice",
        "gfy_val_id": "validate",
        "gfy_login_id": "login",
    },
}


class McpStdioClient:
    def __init__(self, cmd, cwd=None, env=None):
        merged_env = os.environ.copy()
        if env:
            merged_env.update(env)
        self.t_spawn = time.perf_counter()
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
        self.cold_init_ms = 0.0

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
        self.cold_init_ms = (time.perf_counter() - self.t_spawn) * 1000.0

    def call_tool(self, name, arguments):
        return self.rpc("tools/call", {"name": name, "arguments": arguments})

    def rss_mb(self) -> float:
        try:
            p = psutil.Process(self.proc.pid)
            total = p.memory_info().rss
            for c in p.children(recursive=True):
                try:
                    total += c.memory_info().rss
                except psutil.Error:
                    pass
            return round(total / (1024 * 1024), 1)
        except psutil.Error:
            return 0.0

    def close(self):
        try:
            self.proc.terminate()
            self.proc.wait(timeout=3)
        except Exception:
            self.proc.kill()


def benchmark_repo(repo_name, cfg):
    repo_path = cfg["path"]
    graph_json = cfg["graph_json"]

    has_gfy = os.path.exists(graph_json)
    print(f"\n=== Benchmarking MCP Stdio on {repo_name} (has_graphify={has_gfy}) ===", flush=True)
    ce = McpStdioClient([CE_EXE, "mcp", "--root", repo_path], cwd=repo_path)
    gfy = (
        McpStdioClient(
            [sys.executable, "-m", "graphify.serve", graph_json],
            cwd=repo_path,
            env={"GRAPHIFY_MAX_GRAPH_BYTES": "2GB"},
        )
        if has_gfy
        else None
    )
    try:
        ce.initialize()
        if gfy:
            gfy.initialize()

        ce_tools_resp, _, _ = ce.rpc("tools/list", {})
        gfy_tools_resp = gfy.rpc("tools/list", {})[0] if gfy else {}
        gfy_res_resp = gfy.rpc("resources/list", {})[0] if gfy else {}

        ce_tools = [t["name"] for t in ce_tools_resp.get("result", {}).get("tools", [])]
        gfy_tools = [t["name"] for t in gfy_tools_resp.get("result", {}).get("tools", [])]
        gfy_resources = [r["uri"] for r in gfy_res_resp.get("result", {}).get("resources", [])]

        outline_file = cfg["outline_file"]

        results = {
            "repo": repo_name,
            "ce_tool_count": len(ce_tools),
            "ce_tools": ce_tools,
            "graphify_tool_count": len(gfy_tools),
            "graphify_tools": gfy_tools,
            "graphify_resource_count": len(gfy_resources),
            "graphify_resources": gfy_resources,
            "ce_cold_init_ms": round(ce.cold_init_ms, 1),
            "graphify_cold_init_ms": round(gfy.cold_init_ms, 1) if gfy else None,
            "ce_server_rss_mb_after_init": ce.rss_mb(),
            "graphify_server_rss_mb_after_init": gfy.rss_mb() if gfy else None,
            "operations": {},
        }

        ops = [
            (
                "architecture_overview",
                ("get_architecture_map", {}),
                ("god_nodes", {"top_n": 20}),
            ),
            (
                "graph_schema_or_stats_first",
                ("get_taxonomy", {}),
                ("graph_stats", {}),
            ),
            (
                "graph_schema_or_stats_cached",
                ("get_taxonomy", {}),
                ("graph_stats", {}),
            ),
            (
                "symbol_lookup_AuthService_by_name",
                ("find_symbol", {"name": "AuthService"}),
                ("get_node", {"label": "AuthService"}),
            ),
            (
                "symbol_lookup_AuthService_by_id",
                ("get_node_definition", {"symbolId": "AuthService"}),
                ("get_node", {"node_id": cfg["gfy_auth_id"]}),
            ),
            (
                "inbound_callers_validate_by_name",
                ("get_callers", {"target": "validate"}),
                ("get_neighbors", {"label": "validate", "relation_filter": "calls"}),
            ),
            (
                "inbound_callers_validate_by_id",
                ("get_callers", {"target": "validate"}),
                ("get_neighbors", {"node_id": cfg["gfy_val_id"], "relation_filter": "calls"}),
            ),
            (
                "semantic_or_bfs_search",
                ("search_identifiers", {"query": "authentication token validate", "limit": 15}),
                ("query_graph", {"question": "authentication token validate", "depth": 2}),
            ),
            (
                "call_chain_or_shortest_path",
                ("get_call_chain", {"startFunction": cfg["path_src"], "endFunction": cfg["path_tgt"]}),
                ("shortest_path", {"source": cfg["gfy_login_id"], "target": cfg["gfy_val_id"], "undirected": True}),
            ),
            (
                "project_deps_or_community",
                ("get_project_dependencies", {}),
                ("get_community", {"community_id": 0}),
            ),
            (
                "file_outline",
                ("get_file_outline", {"filePath": outline_file}),
                None,
            ),
            (
                "cypher_query_limit_10",
                ("execute_custom_read_cypher", {"query": "MATCH (n:Function) RETURN n.name LIMIT 10"}),
                None,
            ),
            (
                "data_lineage_user",
                ("inspect_data_lineage", {"tableName": "user"}),
                None,
            ),
            (
                "semantic_gates",
                ("check_semantic_gates", {}),
                None,
            ),
        ]

        for op_name, ce_call, gfy_call in ops:
            entry = {}
            if ce_call:
                resp, ms, sz = ce.call_tool(ce_call[0], ce_call[1])
                txt = ""
                try:
                    txt = resp["result"]["content"][0]["text"][:200]
                except Exception:
                    pass
                entry["ce"] = {"tool": ce_call[0], "ms": round(ms, 1), "kb": round(sz / 1024.0, 1), "preview": txt}
            if gfy_call and gfy:
                resp, ms, sz = gfy.call_tool(gfy_call[0], gfy_call[1])
                txt = ""
                try:
                    txt = resp["result"]["content"][0]["text"][:200]
                except Exception:
                    pass
                entry["graphify"] = {"tool": gfy_call[0], "ms": round(ms, 1), "kb": round(sz / 1024.0, 1), "preview": txt}
            results["operations"][op_name] = entry
            print(f"  {op_name}: ce={entry.get('ce', {}).get('ms')}ms | graphify={entry.get('graphify', {}).get('ms')}ms", flush=True)

        results["ce_server_rss_mb_after_queries"] = ce.rss_mb()
        results["graphify_server_rss_mb_after_queries"] = gfy.rss_mb() if gfy else None
        return results
    finally:
        ce.close()
        if gfy:
            gfy.close()


def main():
    all_res = {}
    for repo_name, cfg in REPOS.items():
        all_res[repo_name] = benchmark_repo(repo_name, cfg)

    out_path = r"C:\Work\bench-pilot\results-graphify\mcp-benchmark.json"
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    with open(out_path, "w", encoding="utf-8") as f:
        json.dump(all_res, f, indent=2)
    print(f"\nSaved MCP benchmark telemetry to {out_path}")


if __name__ == "__main__":
    main()
