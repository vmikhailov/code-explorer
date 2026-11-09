import os
import re
import csv
import json
import math
import subprocess
from datetime import datetime

BASE = os.environ.get("BENCH_BASE", ".")
REPOS = os.path.join(BASE, "repos")
GT_DIR = os.path.join(BASE, "groundtruth")
RAW_CE = os.path.join(BASE, "raw", "ce")
RAW_CBM = os.path.join(BASE, "raw", "cbm")
RES_DIR = os.path.join(BASE, "results")
CBM_BIN = os.environ.get("CBM_BIN", "codebase-memory-mcp")
CE_BIN = os.environ.get("CE_BIN", "ce")
os.environ.setdefault("CBM_CACHE_DIR", os.path.join(BASE, ".cache", "cbm"))

def norm_path(p: str, repo: str = "") -> str:
    if not p:
        return ""
    p = p.replace("\\", "/")
    base_norm = BASE.replace("\\", "/").rstrip("/")
    for r_name in ([repo] if repo else ["twenty", "immich"]):
        prefix = f"{base_norm}/repos/{r_name}/"
        if p.lower().startswith(prefix.lower()):
            p = p[len(prefix):]
            break
    return p.lstrip("/")

def run_ce_query(repo: str, cat: str, cypher: str, attempt: int = 1):
    repo_dir = os.path.join(REPOS, repo)
    raw_file = os.path.join(RAW_CE, f"{repo}-{cat}-{attempt}.txt")
    cmd = [CE_BIN, "query", cypher, "--format", "json"]
    res = subprocess.run(cmd, cwd=repo_dir, capture_output=True, text=True, encoding="utf-8", errors="replace")
    out = res.stdout if res.returncode == 0 else (res.stdout + "\n" + res.stderr)
    with open(raw_file, "w", encoding="utf-8") as f:
        f.write(f"# Query: {cypher}\n")
        f.write(out)
    try:
        data = json.loads(res.stdout) if res.returncode == 0 and res.stdout.strip().startswith("[") else []
    except Exception:
        data = []
    return data, f"raw/ce/{repo}-{cat}-{attempt}.txt", res.returncode == 0

def run_cbm_query(repo: str, cat: str, cypher: str, attempt: int = 1):
    proj = os.environ.get(f"CBM_PROJECT_{repo.upper()}", repo)
    raw_file = os.path.join(RAW_CBM, f"{repo}-{cat}-{attempt}.txt")
    cmd = [CBM_BIN, "cli", "query_graph", "--project", proj, "--query", cypher, "--max-rows", "99998", "--max-output-tokens", "1000000", "--format", "json"]
    res = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace")
    out = res.stdout + ("\n" + res.stderr if res.stderr else "")
    with open(raw_file, "w", encoding="utf-8") as f:
        f.write(f"# Query: {cypher}\n")
        f.write(out)
    rows = []
    ok = False
    for line in res.stdout.splitlines():
        line = line.strip()
        if line.startswith("{") and '"rows"' in line:
            try:
                obj = json.loads(line)
                rows = obj.get("rows", [])
                ok = True
            except Exception:
                pass
    return rows, f"raw/cbm/{repo}-{cat}-{attempt}.txt", ok

def val_at(row_dict: dict, idx: int):
    vals = list(row_dict.values())
    return vals[idx] if idx < len(vals) and vals[idx] is not None else ""

def write_tool_csv(tool: str, repo: str, cat: str, items: list, raw_ref: str):
    path = os.path.join(RES_DIR, f"{tool}-{repo}-{cat}.csv")
    headers = ["id", "file", "line", "symbol", "detail", "raw_ref"]
    with open(path, "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=headers)
        w.writeheader()
        for i, it in enumerate(items, 1):
            w.writerow({
                "id": f"{tool}-{repo}-{cat}-{i:04d}",
                "file": norm_path(str(it.get("file", "")), repo),
                "line": it.get("line", "") or "",
                "symbol": it.get("symbol", "") or "",
                "detail": it.get("detail", "") or "",
                "raw_ref": raw_ref
            })
    return items

def load_gt(repo: str, cat: str, full: bool = False):
    suffix = "-full.csv" if full else ".csv"
    path = os.path.join(GT_DIR, f"{repo}-{cat}{suffix}")
    if not os.path.exists(path):
        path = os.path.join(GT_DIR, f"{repo}-{cat}.csv")
    with open(path, "r", encoding="utf-8") as f:
        return list(csv.DictReader(f))

def is_test_or_noise_file(f_path: str) -> bool:
    p = f_path.lower().replace("\\", "/")
    if not p:
        return True
    if any(p.endswith(x) for x in (".spec.ts", ".test.ts", ".spec.tsx", ".test.tsx", "_test.py", ".yml", ".yaml", ".json", ".md", ".mdx", ".graphql")):
        return True
    if any(seg in p.split("/") for seg in ("__tests__", "__mocks__", "test", "tests", "e2e", "testing", "fixtures", "mock-data", "twenty-e2e-testing")):
        return True
    return False

def match_item(gt_row: dict, tool_row: dict, cat: str) -> bool:
    gf = gt_row.get("file", "").lower().strip("/")
    tf = norm_path(str(tool_row.get("file", "")), "").lower().strip("/")
    if gf and tf and gf != tf and not tf.endswith(gf) and not gf.endswith(tf):
        return False

    gs = gt_row.get("symbol", "").lower()
    ts = str(tool_row.get("symbol", "")).lower()
    gd = gt_row.get("detail", "").lower()
    td = str(tool_row.get("detail", "")).lower()

    if cat == "c4":
        # Table match: detail is table name, symbol is Entity/Table class name
        return (gd and (gd == td or gd == ts)) or (gs and (gs == ts or gs == td))

    if cat == "c8-topics":
        # Topic match: resolved_name (or job name) + role + file/symbol
        g_topic = gt_row.get("resolved_name", "").lower().replace("-", "").replace("_", "")
        g_raw_topic = gt_row.get("topic", "").lower()
        g_job = g_raw_topic.split(".")[-1].replace("-", "").replace("_", "") if "." in g_raw_topic else ""
        g_role = gt_row.get("role", "").lower()
        td_norm = td.replace("-", "").replace("_", "")
        topic_ok = (g_topic and g_topic in td_norm) or (g_job and g_job in td_norm)
        return topic_ok and g_role in td and (not gf or gf == tf)

    if cat == "c9-services":
        g_svc = gt_row.get("service", "").lower()
        g_root = gt_row.get("root_path", "").lower()
        return g_svc == ts or (g_root and g_root == tf) or g_svc in ts or ts in g_svc

    if cat == "c9-edges":
        gf_svc = gt_row.get("from_service", "").lower()
        gt_svc = gt_row.get("to_service", "").lower()
        return (gf_svc in ts or gf_svc in td) and (gt_svc in ts or gt_svc in td)

    if cat == "c10-schema":
        g_tbl = gt_row.get("table", "").lower()
        g_name = gt_row.get("name", "").lower()
        return g_tbl in td and (g_name == ts or ts.endswith("." + g_name))

    if cat == "c6":
        g_callee = gd.replace("calls ", "").split(".")[-1].strip()
        t_callee = td.replace("calls ", "").split(".")[-1].strip()
        g_caller = gs.split(".")[-1] if "." in gs else gs
        t_caller = ts.split(".")[-1] if "." in ts else ts
        return (g_callee and g_callee == t_callee) and (g_caller == t_caller or gs in ts or ts in gs)

    # Standard symbol match: Class.method or method name
    g_short = gs.split(".")[-1] if "." in gs else gs
    t_short = ts.split(".")[-1] if "." in ts else ts
    sym_match = (gs and ts and (gs == ts or g_short == t_short or gs in ts or ts in gs))
    if not sym_match and cat == "c1":
        # For C1, route/method match on same file is allowed per §7.1
        if gf and (gf == tf or tf.endswith(gf)) and gd and td and (gd in td or td in gd):
            sym_match = True
    if not sym_match and cat in ("c2", "c5"):
        if gf and (gf == tf or tf.endswith(gf)):
            try:
                gl = int(gt_row.get("line", 0))
                tl = int(tool_row.get("line", 0))
                if gl > 0 and tl > 0 and abs(gl - tl) <= 5:
                    sym_match = True
            except Exception:
                pass
    return sym_match

def evaluate_category(repo: str, cat: str, tool: str, tool_items: list, notes: str = ""):
    gt_sample = load_gt(repo, cat, full=False)
    if not gt_sample:
        return None

    matched = 0
    for g in gt_sample:
        if any(match_item(g, t, cat) for t in tool_items):
            matched += 1

    gt_count = len(gt_sample)
    recall = round((matched * 100) / gt_count) if gt_count > 0 else 0
    part = "Part 1" if cat in ("c1", "c2", "c6", "c7") else "Part 2"

    return {
        "repo": repo,
        "part": part,
        "category": cat,
        "tool": tool,
        "gt_count": gt_count,
        "matched": matched,
        "recall": recall,
        "total_extracted": len(tool_items),
        "notes": notes
    }

scores = []

for repo in ("twenty", "immich"):
    print(f"=== Querying CE and CBM on {repo} ===")

    # ---------------- PART 1: COMMON GROUND (C1, C2, C6, C7) ----------------

    # C1: Incoming Endpoints
    ce_c1_data, ref, _ = run_ce_query(repo, "c1", "MATCH (e:Endpoint)-[:EXPOSED_BY]->(f:Function) RETURN f.file_path, f.start_line, f.name, e.http_method, e.route_template")
    ce_c1_items = [{"file": val_at(r, 0), "line": val_at(r, 1), "symbol": val_at(r, 2), "detail": f"{val_at(r, 3)} {val_at(r, 4)}"} for r in ce_c1_data]
    write_tool_csv("ce", repo, "c1", ce_c1_items, ref)
    scores.append(evaluate_category(repo, "c1", "ce", ce_c1_items, f"EXPOSED_BY endpoints: {len(ce_c1_items)}"))

    cbm_c1_rows, ref_cbm, _ = run_cbm_query(repo, "c1", "MATCH (m)-[:HANDLES]->(r:Route) RETURN m.file_path, m.start_line, m.name, r.name")
    cbm_c1_items = [{"file": r[0], "line": r[1], "symbol": r[2], "detail": r[3]} for r in cbm_c1_rows]
    write_tool_csv("cbm", repo, "c1", cbm_c1_items, ref_cbm)
    scores.append(evaluate_category(repo, "c1", "cbm", cbm_c1_items, f"HANDLES->Route: {len(cbm_c1_items)}"))

    # C2: Outbound HTTP Calls
    ce_c2_data, ref, _ = run_ce_query(repo, "c2", "MATCH (ext:ExternalService)-[r:CALLED_BY]->(f:Function) WHERE COALESCE(ext.protocol, 'http') <> 'graphql' RETURN f.file_path, COALESCE(r.line, f.start_line), f.name, COALESCE(ext.url, ext.name)")
    ce_c2_items = [{"file": val_at(r, 0), "line": val_at(r, 1), "symbol": val_at(r, 2), "detail": val_at(r, 3) or val_at(r, 2)} for r in ce_c2_data]
    ce_c2_ep_data, _, _ = run_ce_query(repo, "c2-ep", "MATCH (f:Function)-[r:CALLS_ENDPOINT]->(ep:Endpoint) WHERE COALESCE(ep.protocol, 'REST') <> 'GraphQL' RETURN f.file_path, COALESCE(r.line, f.start_line), f.name, COALESCE(ep.route_template, ep.name)")
    for r in ce_c2_ep_data:
        ce_c2_items.append({"file": val_at(r, 0), "line": val_at(r, 1), "symbol": val_at(r, 2), "detail": val_at(r, 3) or val_at(r, 2)})
    ce_c2_ext_data, _, _ = run_ce_query(repo, "c2-ext", "MATCH (ext:ExternalService) WHERE COALESCE(ext.protocol, 'http') <> 'graphql' RETURN ext.file_path, ext.start_line, ext.name, COALESCE(ext.url, ext.name)")
    for r in ce_c2_ext_data:
        ce_c2_items.append({"file": val_at(r, 0), "line": val_at(r, 1), "symbol": val_at(r, 2), "detail": val_at(r, 3) or val_at(r, 2)})
    write_tool_csv("ce", repo, "c2", ce_c2_items, ref)
    scores.append(evaluate_category(repo, "c2", "ce", ce_c2_items, f"HTTP call sites + ExternalService: {len(ce_c2_items)}"))

    cbm_c2_rows, ref_cbm, _ = run_cbm_query(repo, "c2", "MATCH (a)-[:HTTP_CALLS]->(b) RETURN a.file_path, a.start_line, a.name, b.name")
    cbm_c2_items = [{"file": r[0], "line": r[1], "symbol": r[2], "detail": r[3]} for r in cbm_c2_rows]
    write_tool_csv("cbm", repo, "c2", cbm_c2_items, ref_cbm)
    scores.append(evaluate_category(repo, "c2", "cbm", cbm_c2_items, f"HTTP_CALLS edges: {len(cbm_c2_items)}"))

    # C6: Call Graph
    gt_c6 = load_gt(repo, "c6", full=False)
    target_fns = sorted({r["detail"].replace("calls ", "").split(".")[-1] for r in gt_c6})
    fn_list_str = ", ".join(f"'{fn}'" for fn in target_fns)
    ce_c6_data, ref, _ = run_ce_query(repo, "c6", f"MATCH (caller:Function)-[:CALLS]->(callee:Function) WHERE callee.name IN [{fn_list_str}] OPTIONAL MATCH (enclosing:Function)-[:DECLARES]->(caller) RETURN caller.file_path, COALESCE(enclosing.start_line, caller.start_line), COALESCE(enclosing.name, caller.name), callee.name")
    ce_c6_items = [{"file": val_at(r, 0), "line": val_at(r, 1), "symbol": val_at(r, 2), "detail": f"calls {val_at(r, 3)}"} for r in ce_c6_data]
    write_tool_csv("ce", repo, "c6", ce_c6_items, ref)
    scores.append(evaluate_category(repo, "c6", "ce", ce_c6_items, f"CALLS for target methods: {len(ce_c6_items)}"))

    cbm_c6_items = []
    for fn in target_fns:
        rows_fn, ref_cbm, _ = run_cbm_query(repo, f"c6-{fn}", f"MATCH (caller)-[:CALLS]->(callee) WHERE callee.name = '{fn}' RETURN caller.file_path, caller.start_line, caller.name, callee.name")
        for r in rows_fn:
            cbm_c6_items.append({"file": r[0], "line": r[1], "symbol": r[2], "detail": f"calls {r[3]}"})
    write_tool_csv("cbm", repo, "c6", cbm_c6_items, f"raw/cbm/{repo}-c6-*.txt")
    scores.append(evaluate_category(repo, "c6", "cbm", cbm_c6_items, f"CALLS for target methods: {len(cbm_c6_items)}"))

    # C7: Inheritance & Implementation
    ce_c7_data, ref, _ = run_ce_query(repo, "c7", "MATCH (c:Type)-[r:INHERITS_FROM|IMPLEMENTS]->(p) RETURN c.file_path, c.start_line, c.name, type(r), p.name")
    ce_c7_items = [{"file": val_at(r, 0), "line": val_at(r, 1), "symbol": val_at(r, 2), "detail": f"{val_at(r, 3)} {val_at(r, 4)}"} for r in ce_c7_data]
    write_tool_csv("ce", repo, "c7", ce_c7_items, ref)
    scores.append(evaluate_category(repo, "c7", "ce", ce_c7_items, f"INHERITS_FROM+IMPLEMENTS: {len(ce_c7_items)}"))

    cbm_c7_rows, ref_cbm, _ = run_cbm_query(repo, "c7", "MATCH (c)-[r:INHERITS|IMPLEMENTS]->(p) RETURN c.file_path, c.start_line, c.name, type(r), p.name")
    cbm_c7_items = [{"file": r[0], "line": r[1], "symbol": r[2], "detail": f"{r[3]} {r[4]}"} for r in cbm_c7_rows]
    write_tool_csv("cbm", repo, "c7", cbm_c7_items, ref_cbm)
    scores.append(evaluate_category(repo, "c7", "cbm", cbm_c7_items, f"INHERITS+IMPLEMENTS: {len(cbm_c7_items)}"))

    # ---------------- PART 2: CE L4/L5 ARCHITECTURAL ONTOLOGY ----------------

    # C3: Queue Producers & Consumers
    ce_c3p_data, ref, _ = run_ce_query(repo, "c3-producers", "MATCH (t:Topic)-[:PUBLISHED_BY]->(f:Function) RETURN f.file_path, f.start_line, f.name, t.name")
    ce_c3p_items = [{"file": val_at(r, 0), "line": val_at(r, 1), "symbol": val_at(r, 2), "detail": val_at(r, 3)} for r in ce_c3p_data]
    write_tool_csv("ce", repo, "c3-producers", ce_c3p_items, ref)
    scores.append(evaluate_category(repo, "c3-producers", "ce", ce_c3p_items, f"PUBLISHED_BY edges: {len(ce_c3p_items)}"))

    ce_c3c_data, ref, _ = run_ce_query(repo, "c3-consumers", "MATCH (t:Topic)-[:SUBSCRIBED_BY]->(m:Function) RETURN m.file_path, m.start_line, m.name, t.name")
    ce_c3c_items = [{"file": val_at(r, 0), "line": val_at(r, 1), "symbol": val_at(r, 2), "detail": val_at(r, 3)} for r in ce_c3c_data]
    ce_c3c_file_data, _, _ = run_ce_query(repo, "c3-consumers-file", "MATCH (t:Topic)-[:SUBSCRIBED_BY]->(f:File)<-[:DECLARED_IN]-(c:Type)-[:HAS_METHOD]->(m:Function) WHERE m.name <> 'constructor' RETURN f.path, m.start_line, m.name, t.name")
    seen_c3c = {(it["file"], it["symbol"], it["detail"]) for it in ce_c3c_items}
    for r in ce_c3c_file_data:
        key = (val_at(r, 0), val_at(r, 2), val_at(r, 3))
        if key not in seen_c3c:
            seen_c3c.add(key)
            ce_c3c_items.append({"file": val_at(r, 0), "line": val_at(r, 1), "symbol": val_at(r, 2), "detail": val_at(r, 3)})
    write_tool_csv("ce", repo, "c3-consumers", ce_c3c_items, ref)
    scores.append(evaluate_category(repo, "c3-consumers", "ce", ce_c3c_items, f"SUBSCRIBED_BY handler methods: {len(ce_c3c_items)}"))

    # C4: DB Tables
    ce_c4_data, ref, _ = run_ce_query(repo, "c4", "MATCH (t:Table) OPTIONAL MATCH (t)-[:IMPLEMENTED_BY]->(ent:Type) RETURN COALESCE(ent.file_path, t.path), ent.start_line, COALESCE(ent.name, t.name), t.name")
    ce_c4_items = [{"file": val_at(r, 0), "line": val_at(r, 1), "symbol": val_at(r, 2), "detail": val_at(r, 3)} for r in ce_c4_data]
    write_tool_csv("ce", repo, "c4", ce_c4_items, ref)
    scores.append(evaluate_category(repo, "c4", "ce", ce_c4_items, f"Table nodes: {len(ce_c4_items)}"))

    # C5: Code -> Table Lineage
    ce_c5_data, ref, _ = run_ce_query(repo, "c5", "MATCH (q:Query)-[r:WRITES_TO|READS_FROM]->(t:Table) OPTIONAL MATCH (q)-[:ACCESSED_BY]->(f:Function) RETURN COALESCE(f.file_path, q.file_path), q.start_line, COALESCE(f.name, q.name), type(r), t.name")
    ce_c5_items = [{"file": val_at(r, 0), "line": val_at(r, 1), "symbol": val_at(r, 2), "detail": f"{val_at(r, 4)}:{'read' if val_at(r, 3)=='READS_FROM' else 'write'}"} for r in ce_c5_data]
    write_tool_csv("ce", repo, "c5", ce_c5_items, ref)
    scores.append(evaluate_category(repo, "c5", "ce", ce_c5_items, f"READS_FROM/WRITES_TO edges: {len(ce_c5_items)}"))

    # C8: Topic Resolution
    ce_c8_items = []
    for it in ce_c3p_items:
        ce_c8_items.append({"file": it["file"], "line": it["line"], "symbol": it["symbol"], "detail": f"producer:{it['detail']}"})
    for it in ce_c3c_items:
        ce_c8_items.append({"file": it["file"], "line": it["line"], "symbol": it["symbol"], "detail": f"consumer:{it['detail']}"})
    write_tool_csv("ce", repo, "c8-topics", ce_c8_items, f"raw/ce/{repo}-c3-*.txt")
    scores.append(evaluate_category(repo, "c8-topics", "ce", ce_c8_items, f"Resolved enum topics (producer+consumer links): {len(ce_c8_items)}"))

    # C9: Services & Inter-Service Edges
    ce_c9s_data, ref, _ = run_ce_query(repo, "c9-services", "MATCH (p:Project) RETURN p.path, labels(p), p.name")
    ce_c9s_items = [{"file": val_at(r, 0), "line": "", "symbol": val_at(r, 2), "detail": str(val_at(r, 1))} for r in ce_c9s_data]
    write_tool_csv("ce", repo, "c9-services", ce_c9s_items, ref)
    scores.append(evaluate_category(repo, "c9-services", "ce", ce_c9s_items, f"Project nodes: {len(ce_c9s_items)}"))

    # C10: DB Schema (Columns / Relations / Entities)
    ce_c10_data, ref, _ = run_ce_query(repo, "c10-schema", "MATCH (t:Table)-[:IMPLEMENTED_BY]->(ent:Type)-[:HAS_MEMBER]->(m:Member) RETURN ent.file_path, m.start_line, m.name, t.name")
    ce_c10_items = [{"file": val_at(r, 0), "line": val_at(r, 1), "symbol": val_at(r, 2), "detail": val_at(r, 3)} for r in ce_c10_data]
    for it in ce_c4_items:
        ce_c10_items.append({"file": it["file"], "line": it["line"], "symbol": it["symbol"], "detail": it["detail"]})
    write_tool_csv("ce", repo, "c10-schema", ce_c10_items, ref)
    scores.append(evaluate_category(repo, "c10-schema", "ce", ce_c10_items, f"Entity+Member links via IMPLEMENTED_BY: {len(ce_c10_items)}"))

    # C11: 6 Architectural Cypher Queries
    c11_queries_ce = [
        ("q1", "MATCH (t:Topic) WHERE NOT (t)-[:SUBSCRIBED_BY]->() OR NOT (t)-[:PUBLISHED_BY]->() RETURN t.name"),
        ("q2", "MATCH (p1:Project)-[:USES_DB]->(d:Database)<-[:USES_DB]-(p2:Project) WHERE p1.name < p2.name RETURN p1.name, p2.name, d.name"),
        ("q3", "MATCH (q:Query)-[:WRITES_TO]->(t:Table) RETURN t.name, count(q) AS writes ORDER BY writes DESC LIMIT 10"),
        ("q4", "MATCH (e:Endpoint)-[:EXPOSED_BY]->(f:Function)-[:CALLS*1..4]->(w:Function)<-[:DECLARED_IN]-(q:Query)-[:WRITES_TO]->(t:Table) RETURN e.name, t.name LIMIT 20"),
        ("q5", "MATCH (p:Project) OPTIONAL MATCH (p)-[:EXPOSES]->(e:Endpoint) OPTIONAL MATCH (p)-[:PUBLISHES_TO]->(tp:Topic) RETURN p.name, count(DISTINCT e), count(DISTINCT tp)"),
        ("q6", "MATCH (e:Endpoint)-[:EXPOSED_BY]->(f:Function)-[:CALLS*0..3]->(pub:Function)<-[:PUBLISHED_BY]-(t:Topic)-[:SUBSCRIBED_BY]->(sub) RETURN e.name, t.name, sub.name LIMIT 20")
    ]
    ce_c11_pass = 0
    ce_c11_rows_total = 0
    for qid, q_cypher in c11_queries_ce:
        q_data, _, ok = run_ce_query(repo, f"c11-{qid}", q_cypher)
        if ok:
            ce_c11_pass += 1
            ce_c11_rows_total += len(q_data)
    scores.append({
        "repo": repo, "part": "Part 2", "category": "c11-queries", "tool": "ce",
        "gt_count": 6, "matched": ce_c11_pass, "recall": round((ce_c11_pass * 100) / 6),
        "total_extracted": ce_c11_rows_total,
        "notes": f"{ce_c11_pass}/6 expressible & executed in single Cypher query"
    })

# Write scores.csv
scores_path = os.path.join(RES_DIR, "scores.csv")
score_headers = ["repo", "part", "category", "tool", "gt_count", "matched", "recall", "total_extracted", "notes"]
with open(scores_path, "w", newline="", encoding="utf-8") as f:
    w = csv.DictWriter(f, fieldnames=score_headers)
    w.writeheader()
    for s in scores:
        if s:
            w.writerow(s)

print(f"Step 6 & 7 complete! Wrote {len(scores)} score rows to {scores_path}")
