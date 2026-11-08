import csv
import json
import os
import re
import subprocess
from collections import Counter

BASE = r"C:\Work\bench-pilot"
REPOS = os.path.join(BASE, "repos")
GT_DIR = os.path.join(BASE, "groundtruth")
RAW_CE = os.path.join(BASE, "raw", "ce")
RAW_GFY = os.path.join(BASE, "raw", "graphify")
RES_DIR = os.path.join(BASE, "results-graphify")
CE_BIN = r"c:\Work\Personal\code-explorer\cli\.Build\bin_Release_AnyCPU\CodeExplorer\ce.exe"

os.makedirs(RAW_CE, exist_ok=True)
os.makedirs(RAW_GFY, exist_ok=True)
os.makedirs(RES_DIR, exist_ok=True)


def norm_path(p: str, repo: str = "") -> str:
    if not p:
        return ""
    p = p.replace("\\", "/")
    for r_name in ([repo] if repo else ["twenty", "immich"]):
        prefix = f"C:/Work/bench-pilot/repos/{r_name}/"
        if p.lower().startswith(prefix.lower()):
            p = p[len(prefix):]
            break
    return p.lstrip("/")


def loc_to_line(loc) -> int:
    if not loc:
        return 0
    m = re.search(r"L(\d+)", str(loc))
    return int(m.group(1)) if m else 0


def clean_sym(label: str) -> str:
    if not label:
        return ""
    s = str(label).strip()
    if s.startswith("."):
        s = s[1:]
    if s.endswith("()"):
        s = s[:-2]
    return s.strip()


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
                "raw_ref": raw_ref,
            })
    return items


def load_gt(repo: str, cat: str, full: bool = False):
    suffix = "-full.csv" if full else ".csv"
    path = os.path.join(GT_DIR, f"{repo}-{cat}{suffix}")
    if not os.path.exists(path):
        path = os.path.join(GT_DIR, f"{repo}-{cat}.csv")
    with open(path, "r", encoding="utf-8") as f:
        return list(csv.DictReader(f))


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
        return (gd and (gd == td or gd == ts)) or (gs and (gs == ts or gs == td))

    if cat == "c8-topics":
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

    g_short = gs.split(".")[-1] if "." in gs else gs
    t_short = ts.split(".")[-1] if "." in ts else ts
    sym_match = bool(gs and ts and (gs == ts or g_short == t_short or gs in ts or ts in gs))
    if not sym_match and cat == "c1":
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
        "notes": notes,
    }


def load_graphify(repo: str):
    gpath = os.path.join(REPOS, repo, "graphify-out", "graph.json")
    if not os.path.exists(gpath):
        return {}, [], lambda nid: ""
    with open(gpath, "r", encoding="utf-8") as f:
        data = json.load(f)
    nodes = {n["id"]: n for n in data.get("nodes", []) if isinstance(n, dict) and "id" in n}
    links = data.get("links", data.get("edges", []))

    # Build owner class map for methods (`Class -[method|contains]-> Method`)
    owner_by_id = {}
    for l in links:
        if l.get("relation") in ("method", "contains"):
            src = nodes.get(l.get("source"))
            tgt = nodes.get(l.get("target"))
            if src and tgt and not src.get("label", "").endswith(".ts") and not src.get("label", "").endswith(".py"):
                owner_by_id[l["target"]] = clean_sym(src.get("label", ""))

    def qualified_name(nid: str) -> str:
        n = nodes.get(nid, {})
        base = clean_sym(n.get("label", ""))
        cls = n.get("_callable_class") or owner_by_id.get(nid)
        if cls and "." not in base:
            return f"{clean_sym(cls)}.{base}"
        return base

    return nodes, links, qualified_name


def main():
    scores = []
    summary_stats = {}

    for repo in ("twenty", "immich"):
        print(f"=== Evaluating CE and Graphify on {repo} ===")
        nodes, links, qname = load_graphify(repo)
        rel_counts = Counter(l.get("relation") for l in links)
        ctx_counts = Counter(l.get("context") for l in links)
        summary_stats[repo] = {
            "nodes": len(nodes),
            "links": len(links),
            "relations": dict(rel_counts.most_common(30)),
            "contexts": dict(ctx_counts.most_common(30)),
        }

        # ---------------- PART 1: C1, C2, C6, C7 ----------------

        # C1: Incoming Endpoints
        ce_c1_data, ref, _ = run_ce_query(
            repo,
            "c1",
            "MATCH (e:Endpoint)-[:EXPOSED_BY]->(f:Function) RETURN f.file_path, f.start_line, f.name, e.http_method, e.route_template",
        )
        ce_c1_items = [
            {"file": val_at(r, 0), "line": val_at(r, 1), "symbol": val_at(r, 2), "detail": f"{val_at(r, 3)} {val_at(r, 4)}"}
            for r in ce_c1_data
        ]
        write_tool_csv("ce", repo, "c1", ce_c1_items, ref)
        scores.append(evaluate_category(repo, "c1", "ce", ce_c1_items, f"EXPOSED_BY endpoints: {len(ce_c1_items)}"))

        # Graphify C1: check if any route/endpoint nodes exist; also check decorator references to HTTP/GQL route decorators
        http_decorators = {"get", "post", "put", "delete", "patch", "query", "mutation", "router"}
        gfy_c1_strict = []  # Graphify has no Endpoint/Route node type with route_template
        gfy_c1_decorator = []
        for l in links:
            if l.get("context") == "decorator":
                tgt_n = nodes.get(l.get("target"), {})
                t_lbl = clean_sym(tgt_n.get("label", "")).lower().split(".")[-1]
                if t_lbl in http_decorators:
                    src_n = nodes.get(l.get("source"), {})
                    sf = l.get("source_file") or src_n.get("source_file", "")
                    sl = loc_to_line(l.get("source_location") or src_n.get("source_location"))
                    gfy_c1_decorator.append({
                        "file": sf,
                        "line": sl,
                        "symbol": qname(l["source"]),
                        "detail": f"@{t_lbl.upper()} (no route path modeled)",
                    })
        write_tool_csv("graphify", repo, "c1", gfy_c1_strict, "graphify-out/graph.json")
        write_tool_csv("graphify-decorator", repo, "c1", gfy_c1_decorator, "graphify-out/graph.json")
        dec_eval = evaluate_category(repo, "c1", "graphify-decorator", gfy_c1_decorator)
        scores.append(
            evaluate_category(
                repo,
                "c1",
                "graphify",
                gfy_c1_strict,
                f"No Endpoint/Route nodes modeled (0); decorator-ref matches={dec_eval['matched']}/{dec_eval['gt_count']} ({len(gfy_c1_decorator)} @Get/@Post/@Query refs without route paths)",
            )
        )

        # C2: Outbound HTTP Calls
        ce_c2_data, ref, _ = run_ce_query(
            repo,
            "c2",
            "MATCH (ext:ExternalService)-[r:CALLED_BY]->(f:Function) WHERE COALESCE(ext.protocol, 'http') <> 'graphql' RETURN f.file_path, COALESCE(r.line, f.start_line), f.name, COALESCE(ext.url, ext.name)",
        )
        ce_c2_items = [
            {"file": val_at(r, 0), "line": val_at(r, 1), "symbol": val_at(r, 2), "detail": val_at(r, 3) or val_at(r, 2)}
            for r in ce_c2_data
        ]
        ce_c2_ep_data, _, _ = run_ce_query(
            repo,
            "c2-ep",
            "MATCH (f:Function)-[r:CALLS_ENDPOINT]->(ep:Endpoint) WHERE COALESCE(ep.protocol, 'REST') <> 'GraphQL' RETURN f.file_path, COALESCE(r.line, f.start_line), f.name, COALESCE(ep.route_template, ep.name)",
        )
        for r in ce_c2_ep_data:
            ce_c2_items.append(
                {"file": val_at(r, 0), "line": val_at(r, 1), "symbol": val_at(r, 2), "detail": val_at(r, 3) or val_at(r, 2)}
            )
        ce_c2_ext_data, _, _ = run_ce_query(
            repo,
            "c2-ext",
            "MATCH (ext:ExternalService) WHERE COALESCE(ext.protocol, 'http') <> 'graphql' RETURN ext.file_path, ext.start_line, ext.name, COALESCE(ext.url, ext.name)",
        )
        for r in ce_c2_ext_data:
            ce_c2_items.append(
                {"file": val_at(r, 0), "line": val_at(r, 1), "symbol": val_at(r, 2), "detail": val_at(r, 3) or val_at(r, 2)}
            )
        write_tool_csv("ce", repo, "c2", ce_c2_items, ref)
        scores.append(evaluate_category(repo, "c2", "ce", ce_c2_items, f"HTTP call sites + ExternalService: {len(ce_c2_items)}"))

        gfy_c2_items = []
        write_tool_csv("graphify", repo, "c2", gfy_c2_items, "graphify-out/graph.json")
        scores.append(evaluate_category(repo, "c2", "graphify", gfy_c2_items, "No outbound HTTP / ExternalService relation modeled (0)"))

        # C6: Call Graph (Intra-Class + DI Service Calls)
        gt_c6 = load_gt(repo, "c6", full=False)
        target_fns = sorted({r["detail"].replace("calls ", "").split(".")[-1] for r in gt_c6})
        target_fns_lower = {fn.lower(): fn for fn in target_fns}
        fn_list_str = ", ".join(f"'{fn}'" for fn in target_fns)
        ce_c6_data, ref, _ = run_ce_query(
            repo,
            "c6",
            f"MATCH (caller:Function)-[:CALLS]->(callee:Function) WHERE callee.name IN [{fn_list_str}] OPTIONAL MATCH (enclosing:Function)-[:DECLARES]->(caller) RETURN caller.file_path, COALESCE(enclosing.start_line, caller.start_line), COALESCE(enclosing.name, caller.name), callee.name",
        )
        ce_c6_items = [
            {"file": val_at(r, 0), "line": val_at(r, 1), "symbol": val_at(r, 2), "detail": f"calls {val_at(r, 3)}"}
            for r in ce_c6_data
        ]
        write_tool_csv("ce", repo, "c6", ce_c6_items, ref)
        scores.append(evaluate_category(repo, "c6", "ce", ce_c6_items, f"CALLS for target methods: {len(ce_c6_items)}"))

        gfy_c6_items = []
        for l in links:
            if l.get("relation") in ("calls", "indirect_call", "dispatches_to"):
                tgt_n = nodes.get(l.get("target"), {})
                callee_short = clean_sym(tgt_n.get("label", "")).split(".")[-1]
                if callee_short.lower() in target_fns_lower:
                    canon_callee = target_fns_lower[callee_short.lower()]
                    src_n = nodes.get(l.get("source"), {})
                    sf = src_n.get("source_file") or l.get("source_file", "")
                    sl = loc_to_line(src_n.get("source_location") or l.get("source_location"))
                    gfy_c6_items.append({
                        "file": sf,
                        "line": sl,
                        "symbol": qname(l["source"]),
                        "detail": f"calls {canon_callee}",
                    })
        write_tool_csv("graphify", repo, "c6", gfy_c6_items, "graphify-out/graph.json")
        scores.append(evaluate_category(repo, "c6", "graphify", gfy_c6_items, f"calls/indirect_call/dispatches_to for target methods: {len(gfy_c6_items)}"))

        # C7: Class Inheritance & Interface Implementation
        ce_c7_data, ref, _ = run_ce_query(
            repo,
            "c7",
            "MATCH (c:Type)-[r:INHERITS_FROM|IMPLEMENTS]->(p) RETURN c.file_path, c.start_line, c.name, type(r), p.name",
        )
        ce_c7_items = [
            {"file": val_at(r, 0), "line": val_at(r, 1), "symbol": val_at(r, 2), "detail": f"{val_at(r, 3)} {val_at(r, 4)}"}
            for r in ce_c7_data
        ]
        write_tool_csv("ce", repo, "c7", ce_c7_items, ref)
        scores.append(evaluate_category(repo, "c7", "ce", ce_c7_items, f"INHERITS_FROM+IMPLEMENTS: {len(ce_c7_items)}"))

        gfy_c7_items = []
        for l in links:
            if l.get("relation") in ("inherits", "implements", "extends", "mixes_in"):
                src_n = nodes.get(l.get("source"), {})
                tgt_n = nodes.get(l.get("target"), {})
                sf = src_n.get("source_file") or l.get("source_file", "")
                sl = loc_to_line(src_n.get("source_location") or l.get("source_location"))
                gfy_c7_items.append({
                    "file": sf,
                    "line": sl,
                    "symbol": clean_sym(src_n.get("label", "")),
                    "detail": f"{l.get('relation').upper()} {clean_sym(tgt_n.get('label', ''))}",
                })
        write_tool_csv("graphify", repo, "c7", gfy_c7_items, "graphify-out/graph.json")
        scores.append(evaluate_category(repo, "c7", "graphify", gfy_c7_items, f"inherits+implements+extends+mixes_in: {len(gfy_c7_items)}"))

        # ---------------- PART 2: CE L4/L5 ARCHITECTURAL ONTOLOGY ----------------
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

        ce_c4_data, ref, _ = run_ce_query(repo, "c4", "MATCH (t:Table) OPTIONAL MATCH (t)-[:IMPLEMENTED_BY]->(ent:Type) RETURN COALESCE(ent.file_path, t.path), ent.start_line, COALESCE(ent.name, t.name), t.name")
        ce_c4_items = [{"file": val_at(r, 0), "line": val_at(r, 1), "symbol": val_at(r, 2), "detail": val_at(r, 3)} for r in ce_c4_data]
        write_tool_csv("ce", repo, "c4", ce_c4_items, ref)
        scores.append(evaluate_category(repo, "c4", "ce", ce_c4_items, f"Table nodes: {len(ce_c4_items)}"))

        ce_c5_data, ref, _ = run_ce_query(repo, "c5", "MATCH (q:Query)-[r:WRITES_TO|READS_FROM]->(t:Table) OPTIONAL MATCH (q)-[:ACCESSED_BY]->(f:Function) RETURN COALESCE(f.file_path, q.file_path), q.start_line, COALESCE(f.name, q.name), type(r), t.name")
        ce_c5_items = [{"file": val_at(r, 0), "line": val_at(r, 1), "symbol": val_at(r, 2), "detail": f"{val_at(r, 4)}:{'read' if val_at(r, 3)=='READS_FROM' else 'write'}"} for r in ce_c5_data]
        write_tool_csv("ce", repo, "c5", ce_c5_items, ref)
        scores.append(evaluate_category(repo, "c5", "ce", ce_c5_items, f"READS_FROM/WRITES_TO edges: {len(ce_c5_items)}"))

        ce_c8_items = []
        for it in ce_c3p_items:
            ce_c8_items.append({"file": it["file"], "line": it["line"], "symbol": it["symbol"], "detail": f"producer:{it['detail']}"})
        for it in ce_c3c_items:
            ce_c8_items.append({"file": it["file"], "line": it["line"], "symbol": it["symbol"], "detail": f"consumer:{it['detail']}"})
        write_tool_csv("ce", repo, "c8-topics", ce_c8_items, f"raw/ce/{repo}-c3-*.txt")
        scores.append(evaluate_category(repo, "c8-topics", "ce", ce_c8_items, f"Resolved enum topics (producer+consumer links): {len(ce_c8_items)}"))

        ce_c9s_data, ref, _ = run_ce_query(repo, "c9-services", "MATCH (p:Project) RETURN p.path, labels(p), p.name")
        ce_c9s_items = [{"file": val_at(r, 0), "line": "", "symbol": val_at(r, 2), "detail": str(val_at(r, 1))} for r in ce_c9s_data]
        write_tool_csv("ce", repo, "c9-services", ce_c9s_items, ref)
        scores.append(evaluate_category(repo, "c9-services", "ce", ce_c9s_items, f"Project nodes: {len(ce_c9s_items)}"))

        ce_c10_data, ref, _ = run_ce_query(repo, "c10-schema", "MATCH (t:Table)-[:IMPLEMENTED_BY]->(ent:Type)-[:HAS_MEMBER]->(m:Member) RETURN ent.file_path, m.start_line, m.name, t.name")
        ce_c10_items = [{"file": val_at(r, 0), "line": val_at(r, 1), "symbol": val_at(r, 2), "detail": val_at(r, 3)} for r in ce_c10_data]
        for it in ce_c4_items:
            ce_c10_items.append({"file": it["file"], "line": it["line"], "symbol": it["symbol"], "detail": it["detail"]})
        write_tool_csv("ce", repo, "c10-schema", ce_c10_items, ref)
        scores.append(evaluate_category(repo, "c10-schema", "ce", ce_c10_items, f"Entity+Member links via IMPLEMENTED_BY: {len(ce_c10_items)}"))

        c11_queries_ce = [
            ("q1", "MATCH (t:Topic) WHERE NOT (t)-[:SUBSCRIBED_BY]->() OR NOT (t)-[:PUBLISHED_BY]->() RETURN t.name"),
            ("q2", "MATCH (p1:Project)-[:USES_DB]->(d:Database)<-[:USES_DB]-(p2:Project) WHERE p1.name < p2.name RETURN p1.name, p2.name, d.name"),
            ("q3", "MATCH (q:Query)-[:WRITES_TO]->(t:Table) RETURN t.name, count(q) AS writes ORDER BY writes DESC LIMIT 10"),
            ("q4", "MATCH (e:Endpoint)-[:EXPOSED_BY]->(f:Function)-[:CALLS*1..4]->(w:Function)<-[:DECLARED_IN]-(q:Query)-[:WRITES_TO]->(t:Table) RETURN e.name, t.name LIMIT 20"),
            ("q5", "MATCH (p:Project) OPTIONAL MATCH (p)-[:EXPOSES]->(e:Endpoint) OPTIONAL MATCH (p)-[:PUBLISHES_TO]->(tp:Topic) RETURN p.name, count(DISTINCT e), count(DISTINCT tp)"),
            ("q6", "MATCH (e:Endpoint)-[:EXPOSED_BY]->(f:Function)-[:CALLS*0..3]->(pub:Function)<-[:PUBLISHED_BY]-(t:Topic)-[:SUBSCRIBED_BY]->(sub) RETURN e.name, t.name, sub.name LIMIT 20"),
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
            "notes": f"{ce_c11_pass}/6 expressible & executed in single Cypher query",
        })

        # Also check C4 (SQL DDL tables in graphify vs ORM + SQL tables in CE) and C9-services (package manifests)
        gfy_c4_items = []
        for n in nodes.values():
            sf = str(n.get("source_file", ""))
            if sf.endswith(".sql") or n.get("type") == "table":
                lbl = clean_sym(n.get("label", ""))
                if lbl and not lbl.endswith(".sql"):
                    gfy_c4_items.append({
                        "file": sf,
                        "line": loc_to_line(n.get("source_location")),
                        "symbol": lbl,
                        "detail": lbl,
                    })
        gfy_c4_eval = evaluate_category(repo, "c4", "graphify", gfy_c4_items, f"SQL DDL nodes from .sql files: {len(gfy_c4_items)}")
        summary_stats[repo]["c4_sql_eval"] = gfy_c4_eval

    # Save scores.csv
    scores_csv = os.path.join(RES_DIR, "scores.csv")
    with open(scores_csv, "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(
            f,
            fieldnames=["repo", "part", "category", "tool", "gt_count", "matched", "recall", "total_extracted", "notes"],
        )
        w.writeheader()
        for s in scores:
            if s:
                w.writerow(s)

    with open(os.path.join(RES_DIR, "graphify-summary.json"), "w", encoding="utf-8") as f:
        json.dump({"scores": scores, "stats": summary_stats}, f, indent=2)

    print("\n=== SCORES SUMMARY ===")
    for s in scores:
        print(f"  {s['repo']:7s} | {s['category']:4s} | {s['tool']:10s} | {s['matched']:2d}/{s['gt_count']:2d} (Recall {s['recall']:3d}) | total={s['total_extracted']} | {s['notes']}")


if __name__ == "__main__":
    main()
