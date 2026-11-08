import json
import os
import shutil
import subprocess
import sys
import threading
import time
from pathlib import Path
import psutil

sys.stdout.reconfigure(encoding="utf-8")

CE_BIN = r"c:\Work\Personal\code-explorer\cli\.Build\bin_Release_AnyCPU\CodeExplorer\ce.exe"
REPOS = {
    "immich": r"C:\Work\bench-pilot\repos\immich",
    "twenty": r"C:\Work\bench-pilot\repos\twenty",
}
OUT_JSON = r"C:\Work\bench-pilot\results\graphify-indexing-telemetry.json"


def run_with_tree_memory(cmd, cwd=None, env=None):
    merged_env = os.environ.copy()
    if env:
        merged_env.update(env)

    peak_rss_bytes = 0
    stop_event = threading.Event()
    t0 = time.perf_counter()

    proc = subprocess.Popen(
        cmd,
        cwd=cwd,
        env=merged_env,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        encoding="utf-8",
        errors="replace",
    )

    def sampler():
        nonlocal peak_rss_bytes
        try:
            parent = psutil.Process(proc.pid)
        except psutil.Error:
            return
        while not stop_event.is_set():
            try:
                procs = [parent] + parent.children(recursive=True)
                total = 0
                for p in procs:
                    try:
                        total += p.memory_info().rss
                    except psutil.Error:
                        pass
                if total > peak_rss_bytes:
                    peak_rss_bytes = total
            except psutil.Error:
                pass
            time.sleep(0.2)

    t = threading.Thread(target=sampler, daemon=True)
    t.start()
    out_lines = []
    for line in proc.stdout:
        out_lines.append(line)
        if any(k in line for k in ("[graphify timing]", "wrote ", "Successfully indexed", "[Indexer")):
            print("  ", line.rstrip(), flush=True)
    proc.wait()
    stop_event.set()
    t.join(timeout=1.0)
    elapsed_s = time.perf_counter() - t0
    return proc.returncode, elapsed_s, peak_rss_bytes / (1024 * 1024), "".join(out_lines)


def dir_size_mb(path: Path) -> float:
    if not path.exists():
        return 0.0
    total = sum(f.stat().st_size for f in path.rglob("*") if f.is_file())
    return round(total / (1024 * 1024), 2)


def file_size_mb(path: Path) -> float:
    if not path.exists():
        return 0.0
    return round(path.stat().st_size / (1024 * 1024), 2)


def main():
    telemetry = {}
    for repo_name, repo_path in REPOS.items():
        repo_dir = Path(repo_path)
        telemetry[repo_name] = {}

        # 1. Cold CE Scan
        print(f"\n=== [CE] Cold Scan on {repo_name} ===", flush=True)
        ce_db_dir = repo_dir / ".codeexplorer"
        for f in ce_db_dir.glob("graph.db*"):
            try:
                f.unlink()
            except OSError:
                pass

        rc, elapsed_s, peak_mb, out = run_with_tree_memory(
            [CE_BIN, "scan", str(repo_dir), "--clear"],
            cwd=str(repo_dir),
        )
        db_mb = round(sum(f.stat().st_size for f in ce_db_dir.glob("graph.db*")) / (1024 * 1024), 2)
        telemetry[repo_name]["ce"] = {
            "returncode": rc,
            "wall_s": round(elapsed_s, 2),
            "peak_ram_mb": round(peak_mb, 1),
            "db_size_mb": db_mb,
            "log_tail": out[-2000:],
        }
        print(f"[CE {repo_name}] wall={elapsed_s:.2f}s peak_ram={peak_mb:.1f}MB db={db_mb:.2f}MB", flush=True)

        # 2. Cold Graphify Extract (--code-only --force --timing)
        print(f"\n=== [Graphify] Cold Extract on {repo_name} ===", flush=True)
        g_out_dir = repo_dir / "graphify-out"
        if g_out_dir.exists():
            shutil.rmtree(g_out_dir, ignore_errors=True)

        rc, elapsed_s, peak_mb, out = run_with_tree_memory(
            [sys.executable, "-m", "graphify", "extract", str(repo_dir), "--code-only", "--force", "--timing"],
            cwd=str(repo_dir),
            env={"GRAPHIFY_NO_TIPS": "1", "GRAPHIFY_NO_CTA": "1"},
        )
        graph_json = g_out_dir / "graph.json"
        graph_json_mb = file_size_mb(graph_json)
        total_out_mb = dir_size_mb(g_out_dir)

        nodes_count = 0
        edges_count = 0
        communities_count = 0
        if graph_json.exists():
            with open(graph_json, encoding="utf-8") as gf:
                gd = json.load(gf)
            nodes_count = len(gd.get("nodes", []))
            edges_count = len(gd.get("links", gd.get("edges", [])))
            communities_count = len({n.get("community") for n in gd.get("nodes", []) if n.get("community") is not None})

        telemetry[repo_name]["graphify"] = {
            "returncode": rc,
            "wall_s": round(elapsed_s, 2),
            "peak_ram_mb": round(peak_mb, 1),
            "graph_json_mb": graph_json_mb,
            "total_out_dir_mb": total_out_mb,
            "nodes": nodes_count,
            "edges": edges_count,
            "communities": communities_count,
            "log": out,
        }
        print(
            f"[Graphify {repo_name}] wall={elapsed_s:.2f}s peak_ram={peak_mb:.1f}MB "
            f"graph.json={graph_json_mb:.2f}MB total_dir={total_out_mb:.2f}MB "
            f"nodes={nodes_count} edges={edges_count} communities={communities_count}",
            flush=True,
        )

    Path(OUT_JSON).parent.mkdir(parents=True, exist_ok=True)
    with open(OUT_JSON, "w", encoding="utf-8") as f:
        json.dump(telemetry, f, indent=2)
    print(f"\nSaved indexing telemetry to {OUT_JSON}", flush=True)


if __name__ == "__main__":
    main()
