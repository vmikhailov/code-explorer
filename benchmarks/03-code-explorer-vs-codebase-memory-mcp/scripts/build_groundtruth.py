import os
import re
import csv
import math
import hashlib
from datetime import datetime, timezone

BASE = os.environ.get("BENCH_BASE", ".")
REPOS = os.path.join(BASE, "repos")
GT_DIR = os.path.join(BASE, "groundtruth")
os.makedirs(GT_DIR, exist_ok=True)

def is_test_path(rel_path: str) -> bool:
    p = rel_path.replace("\\", "/").lower()
    if any(p.endswith(ext) for ext in (".spec.ts", ".spec.tsx", ".test.ts", ".test.tsx", "_test.py")):
        return True
    parts = p.split("/")
    if any(seg in ("__tests__", "__mocks__", "test", "tests", "e2e", "testing", "fixtures") for seg in parts):
        return True
    if os.path.basename(p).startswith("test_"):
        return True
    return False

def iter_files(repo_root: str, subdirs, exts):
    for sub in subdirs:
        root_dir = os.path.join(repo_root, sub)
        if not os.path.exists(root_dir):
            continue
        for dirpath, dirnames, filenames in os.walk(root_dir):
            dirnames[:] = [d for d in dirnames if d not in ("node_modules", ".git", "dist", "build", ".yarn", ".codeexplorer", "__pycache__")]
            for fn in sorted(filenames):
                if any(fn.endswith(e) for e in exts):
                    full = os.path.join(dirpath, fn)
                    rel = os.path.relpath(full, repo_root).replace("\\", "/")
                    if not is_test_path(rel):
                        yield full, rel

def write_csv_and_sample(prefix: str, rows: list, headers=("id", "file", "line", "symbol", "detail")):
    # Sort deterministically by file, then line
    rows_sorted = sorted(rows, key=lambda r: (r["file"], int(r["line"]), r.get("symbol", "")))
    full_path = os.path.join(GT_DIR, f"{prefix}-full.csv")
    sample_path = os.path.join(GT_DIR, f"{prefix}.csv")

    with open(full_path, "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=headers)
        w.writeheader()
        for idx, r in enumerate(rows_sorted, 1):
            row_copy = dict(r)
            if "id" in headers:
                row_copy["id"] = f"{prefix}-{idx:04d}"
            w.writerow({k: row_copy.get(k, "") for k in headers})

    n = len(rows_sorted)
    if n > 20:
        step = math.ceil(n / 20)
        sampled = [rows_sorted[i] for i in range(0, n, step)]
    else:
        sampled = list(rows_sorted)

    sampled_out = []
    with open(sample_path, "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=headers)
        w.writeheader()
        for idx, r in enumerate(sampled, 1):
            row_copy = dict(r)
            if "id" in headers:
                row_copy["id"] = f"{prefix}-S{idx:02d}"
            w.writerow({k: row_copy.get(k, "") for k in headers})
            sampled_out.append(row_copy)

    return rows_sorted, sampled_out

def find_enclosing_method_and_class(lines, target_idx):
    cls_name = "UnknownClass"
    for i in range(target_idx, -1, -1):
        m_cls = re.match(r"^\s*(?:export\s+)?(?:abstract\s+)?class\s+([A-Za-z0-9_]+)", lines[i])
        if m_cls:
            cls_name = m_cls.group(1)
            break
    method_name = "unknownMethod"
    reserved = ("if", "for", "while", "switch", "catch", "return", "super", "constructor", "async", "function", "fetch", "add", "addCron", "queue", "queueAll")
    # Search upwards for method signature if target_idx is inside a body, or downwards if target_idx is a decorator
    if lines[target_idx].lstrip().startswith("@"):
        for j in range(target_idx + 1, min(len(lines), target_idx + 25)):
            line_s = lines[j].strip()
            if not line_s or line_s.startswith("@") or line_s.startswith("//") or line_s.startswith("*"):
                continue
            m_fn = re.match(r"^(?:public\s+|private\s+|protected\s+|readonly\s+|async\s+|static\s+)*([A-Za-z0-9_]+)\s*(?:<[^>]*>)?\s*\(", line_s)
            if m_fn and m_fn.group(1) not in reserved:
                method_name = m_fn.group(1)
                break
    else:
        for j in range(target_idx, max(-1, target_idx - 180), -1):
            raw_line = lines[j]
            if raw_line.strip().endswith(";"):
                continue
            m_fn = re.match(r"^  (?:public\s+|private\s+|protected\s+|readonly\s+|async\s+|static\s+)*([A-Za-z0-9_]+)\s*(?:<[^>]*>)?\s*\(", raw_line)
            if m_fn and m_fn.group(1) not in reserved:
                method_name = m_fn.group(1)
                break
    return cls_name, method_name

# ==========================================
# C1: Endpoints (REST & GraphQL & FastAPI)
# ==========================================
def extract_c1_ts(repo_root, subdirs):
    rows = []
    for full, rel in iter_files(repo_root, subdirs, (".ts",)):
        with open(full, "r", encoding="utf-8", errors="ignore") as f:
            content = f.read()
        m_c = re.search(r"@(Controller|Resolver)\s*\(([^\n]*)", content)
        if not m_c:
            continue
        lines = content.splitlines()
        ctrl_kind = m_c.group(1)
        ctrl_arg = m_c.group(2).strip().rstrip(")").strip("'\"")
        for idx, line in enumerate(lines):
            m_m = re.match(r"^\s*@(Get|Post|Put|Patch|Delete|Query|Mutation|Subscription)\s*\(", line)
            if m_m:
                dec_kind = m_m.group(1)
                if dec_kind == "Query" and ctrl_kind == "Controller":
                    continue
                if re.search(r"\)\s*[a-zA-Z0-9_]+\s*:", line):
                    continue
                cls_name, method_name = find_enclosing_method_and_class(lines, idx)
                if method_name == "unknownMethod":
                    continue
                m_line = idx + 1
                for j in range(idx + 1, min(len(lines), idx + 25)):
                    if re.search(rf"\b{re.escape(method_name)}\s*(?:<[^>]*>)?\s*\(", lines[j]):
                        m_line = j + 1
                        break
                detail = f"{dec_kind.upper()} {ctrl_arg}".strip()
                rows.append({
                    "file": rel,
                    "line": m_line,
                    "symbol": f"{cls_name}.{method_name}",
                    "detail": detail
                })
    return rows

def extract_c1_immich(repo_root):
    rows = extract_c1_ts(repo_root, ["server/src"])
    # Add Python FastAPI endpoints in machine-learning/immich_ml
    for full, rel in iter_files(repo_root, ["machine-learning/immich_ml"], (".py",)):
        with open(full, "r", encoding="utf-8", errors="ignore") as f:
            lines = f.read().splitlines()
        for idx, line in enumerate(lines):
            m = re.search(r"^\s*@(?:app|router)\.(get|post|put|delete|patch)\(\s*['\"]([^'\"]+)['\"]", line)
            if m:
                verb = m.group(1).upper()
                route_path = m.group(2)
                fn_name = "unknown"
                fn_line = idx + 1
                for j in range(idx + 1, min(len(lines), idx + 10)):
                    m_fn = re.search(r"^\s*(?:async\s+)?def\s+([A-Za-z0-9_]+)\s*\(", lines[j])
                    if m_fn:
                        fn_name = m_fn.group(1)
                        fn_line = j + 1
                        break
                rows.append({
                    "file": rel,
                    "line": fn_line,
                    "symbol": fn_name,
                    "detail": f"{verb} {route_path}"
                })
    return rows

# ==========================================
# C2: Outbound HTTP calls
# ==========================================
def extract_c2(repo_root, subdirs):
    rows = []
    for full, rel in iter_files(repo_root, subdirs, (".ts",)):
        with open(full, "r", encoding="utf-8", errors="ignore") as f:
            lines = f.read().splitlines()
        for idx, line in enumerate(lines):
            s = line.strip()
            if s.startswith("//") or s.startswith("*"):
                continue
            m = re.search(r"\b(?:await\s+)?(?:fetch|axios\.(?:get|post|put|delete|patch|request)|this\.httpService\.(?:get|post|put|delete|patch|request))\s*(?:<[^>]*>)?\s*\(([^;]*)", line)
            if m:
                # Exclude imapClient.fetch or digestClient.fetch if preceded by dot other than axios/httpService
                if "imapClient.fetch" in line:
                    continue
                cls_name, method_name = find_enclosing_method_and_class(lines, idx)
                arg_preview = m.group(1).strip()[:60]
                rows.append({
                    "file": rel,
                    "line": idx + 1,
                    "symbol": f"{cls_name}.{method_name}",
                    "detail": arg_preview
                })
    return rows

# ==========================================
# C3 & C8: Queues / Async Tasks
# ==========================================
def parse_enum_map(file_path, enum_name):
    with open(file_path, "r", encoding="utf-8", errors="ignore") as f:
        content = f.read()
    m = re.search(rf"export\s+enum\s+{enum_name}\s*\{{([^}}]+)\}}", content, re.S)
    res = {}
    if m:
        for line in m.group(1).splitlines():
            m_kv = re.search(r"([A-Za-z0-9_]+)\s*=\s*['\"]([^'\"]+)['\"]", line)
            if m_kv:
                res[m_kv.group(1)] = m_kv.group(2)
    return res

def extract_c3_c8_twenty(repo_root):
    mq_file = os.path.join(repo_root, "packages/twenty-server/src/engine/core-modules/message-queue/message-queue.constants.ts")
    mq_map = parse_enum_map(mq_file, "MessageQueue")
    producers = []
    consumers = []
    topics = []

    for full, rel in iter_files(repo_root, ["packages/twenty-server/src"], (".ts",)):
        with open(full, "r", encoding="utf-8", errors="ignore") as f:
            content = f.read()
        lines = content.splitlines()

        # Check injected queues in constructor: @InjectMessageQueue(MessageQueue.xyz) private readonly foo: MessageQueueService
        injected_queues = {}
        for m_inj in re.finditer(r"@InjectMessageQueue\(\s*MessageQueue\.([A-Za-z0-9_]+)\s*\)\s*(?:private|protected|public)?\s*(?:readonly)?\s*([A-Za-z0-9_]+)", content):
            injected_queues[m_inj.group(2)] = m_inj.group(1)

        # Producers: .add( or .addCron(
        for idx, line in enumerate(lines):
            m_add = re.search(r"this\.([A-Za-z0-9_]+)\.(?:add|addCron)\s*(?:<[^>]*>)?\s*\(", line)
            if m_add and m_add.group(1) in injected_queues:
                q_key = injected_queues[m_add.group(1)]
                q_val = mq_map.get(q_key, q_key)
                cls_name, method_name = find_enclosing_method_and_class(lines, idx)
                producers.append({
                    "file": rel,
                    "line": idx + 1,
                    "symbol": f"{cls_name}.{method_name}",
                    "detail": q_val
                })
                topics.append({
                    "topic": f"MessageQueue.{q_key}",
                    "resolved_name": q_val,
                    "definition_kind": "enum",
                    "role": "producer",
                    "file": rel,
                    "line": idx + 1,
                    "symbol": f"{cls_name}.{method_name}"
                })

        # Consumers: @Processor(MessageQueue.xyz) or @Processor({ queueName: MessageQueue.xyz ... })
        m_proc = re.search(r"@Processor\(\s*(?:\{\s*queueName:\s*)?MessageQueue\.([A-Za-z0-9_]+)", content)
        if m_proc:
            q_key = m_proc.group(1)
            q_val = mq_map.get(q_key, q_key)
            for idx, line in enumerate(lines):
                if re.search(r"^\s*@Process\(", line):
                    cls_name, method_name = find_enclosing_method_and_class(lines, idx)
                    m_line = idx + 1
                    for j in range(idx + 1, min(len(lines), idx + 10)):
                        if re.search(rf"\b{re.escape(method_name)}\s*\(", lines[j]):
                            m_line = j + 1
                            break
                    consumers.append({
                        "file": rel,
                        "line": m_line,
                        "symbol": f"{cls_name}.{method_name}",
                        "detail": q_val
                    })
                    topics.append({
                        "topic": f"MessageQueue.{q_key}",
                        "resolved_name": q_val,
                        "definition_kind": "enum",
                        "role": "consumer",
                        "file": rel,
                        "line": m_line,
                        "symbol": f"{cls_name}.{method_name}"
                    })
    return producers, consumers, topics

def extract_c3_c8_immich(repo_root):
    enum_file = os.path.join(repo_root, "server/src/enum.ts")
    q_map = parse_enum_map(enum_file, "QueueName")
    j_map = parse_enum_map(enum_file, "JobName")
    producers = []
    consumers = []
    topics = []

    # First pass: map JobName -> QueueName from @OnJob({ name: JobName.X, queue: QueueName.Y })
    job_to_queue = {}
    for full, rel in iter_files(repo_root, ["server/src"], (".ts",)):
        with open(full, "r", encoding="utf-8", errors="ignore") as f:
            content = f.read()
        for m in re.finditer(r"@OnJob\(\{\s*name:\s*JobName\.([A-Za-z0-9_]+)\s*,\s*queue:\s*QueueName\.([A-Za-z0-9_]+)\s*\}\)", content):
            job_to_queue[m.group(1)] = m.group(2)

    for full, rel in iter_files(repo_root, ["server/src"], (".ts",)):
        with open(full, "r", encoding="utf-8", errors="ignore") as f:
            lines = f.read().splitlines()
        for idx, line in enumerate(lines):
            # Consumer: @OnJob({ name: JobName.X, queue: QueueName.Y })
            m_on = re.search(r"@OnJob\(\{\s*name:\s*JobName\.([A-Za-z0-9_]+)\s*,\s*queue:\s*QueueName\.([A-Za-z0-9_]+)\s*\}\)", line)
            if m_on:
                j_key, q_key = m_on.group(1), m_on.group(2)
                q_val = q_map.get(q_key, q_key)
                j_val = j_map.get(j_key, j_key)
                cls_name, method_name = find_enclosing_method_and_class(lines, idx)
                m_line = idx + 1
                for j in range(idx + 1, min(len(lines), idx + 10)):
                    if re.search(rf"\b{re.escape(method_name)}\s*\(", lines[j]):
                        m_line = j + 1
                        break
                consumers.append({
                    "file": rel,
                    "line": m_line,
                    "symbol": f"{cls_name}.{method_name}",
                    "detail": f"{q_val}:{j_val}"
                })
                topics.append({
                    "topic": f"QueueName.{q_key}/JobName.{j_key}",
                    "resolved_name": q_val,
                    "definition_kind": "enum",
                    "role": "consumer",
                    "file": rel,
                    "line": m_line,
                    "symbol": f"{cls_name}.{method_name}"
                })
            # Producer: jobRepository.queue({ name: JobName.X ... }) or queueAll
            if "jobRepository.queue" in line:
                window = " ".join(lines[idx:min(len(lines), idx + 5)])
                m_jb = re.search(r"name:\s*JobName\.([A-Za-z0-9_]+)", window)
                if m_jb:
                    j_key = m_jb.group(1)
                    q_key = job_to_queue.get(j_key, j_key)
                    q_val = q_map.get(q_key, j_map.get(j_key, j_key))
                    j_val = j_map.get(j_key, j_key)
                    cls_name, method_name = find_enclosing_method_and_class(lines, idx)
                    producers.append({
                        "file": rel,
                        "line": idx + 1,
                        "symbol": f"{cls_name}.{method_name}",
                        "detail": f"{q_val}:{j_val}"
                    })
                    topics.append({
                        "topic": f"QueueName.{q_key}/JobName.{j_key}",
                        "resolved_name": q_val,
                        "definition_kind": "enum",
                        "role": "producer",
                        "file": rel,
                        "line": idx + 1,
                        "symbol": f"{cls_name}.{method_name}"
                    })
    return producers, consumers, topics

# ==========================================
# C4 & C10: DB Tables & Schema
# ==========================================
def extract_c4_twenty(repo_root):
    rows = []
    for full, rel in iter_files(repo_root, ["packages/twenty-server/src"], (".ts",)):
        with open(full, "r", encoding="utf-8", errors="ignore") as f:
            content = f.read()
        if "@Entity(" not in content:
            continue
        lines = content.splitlines()
        for idx, line in enumerate(lines):
            if re.match(r"^\s*@Entity\(", line):
                window = " ".join(lines[idx:min(len(lines), idx + 8)])
                m = re.search(r"@Entity\(\s*(?:\{[^}]*?\bname:\s*['\"]([^'\"]+)['\"]|['\"]([^'\"]+)['\"])?", window)
                t_name = (m.group(1) or m.group(2) or "") if m else ""
                cls_name = "UnknownEntity"
                c_line = idx + 1
                for j in range(idx + 1, min(len(lines), idx + 45)):
                    m_c = re.search(r"export\s+(?:abstract\s+)?class\s+([A-Za-z0-9_]+)", lines[j])
                    if m_c:
                        cls_name = m_c.group(1)
                        c_line = j + 1
                        break
                if cls_name != "UnknownEntity":
                    rows.append({
                        "file": rel,
                        "line": c_line,
                        "symbol": cls_name,
                        "detail": t_name or cls_name
                    })
    return rows

def extract_c4_immich(repo_root):
    rows = []
    for full, rel in iter_files(repo_root, ["server/src/schema/tables"], (".ts",)):
        with open(full, "r", encoding="utf-8", errors="ignore") as f:
            lines = f.read().splitlines()
        for idx, line in enumerate(lines):
            m = re.search(r"@Table\(\s*(?:\{\s*name:\s*['\"]([^'\"]+)['\"]|['\"]([^'\"]+)['\"])", line)
            if m:
                t_name = m.group(1) or m.group(2)
                cls_name = "UnknownTable"
                c_line = idx + 1
                for j in range(idx + 1, min(len(lines), idx + 30)):
                    m_c = re.search(r"export\s+class\s+([A-Za-z0-9_]+)", lines[j])
                    if m_c:
                        cls_name = m_c.group(1)
                        c_line = j + 1
                        break
                rows.append({
                    "file": rel,
                    "line": c_line,
                    "symbol": cls_name,
                    "detail": t_name
                })
    return rows

def extract_c10_schema(repo_root, c4_sample):
    schema_rows = []
    for item in c4_sample[:5]:
        t_name = item["detail"]
        rel = item["file"]
        cls_name = item["symbol"]
        full = os.path.join(repo_root, rel.replace("/", os.sep))
        schema_rows.append({
            "table": t_name,
            "kind": "entity",
            "name": cls_name,
            "target": t_name,
            "file": rel,
            "line": item["line"]
        })
        with open(full, "r", encoding="utf-8", errors="ignore") as f:
            lines = f.read().splitlines()
        # Find start of class
        start_idx = max(0, int(item["line"]) - 1)
        for idx in range(start_idx, len(lines)):
            line = lines[idx]
            if idx > start_idx and re.search(r"^export\s+class\s+", line):
                break
            # Check column / relation decorators
            if re.search(r"@(PrimaryGeneratedColumn|Column|CreateDateColumn|UpdateDateColumn|DeleteDateColumn|UpdateIdColumn)\b", line):
                for j in range(idx + 1, min(len(lines), idx + 6)):
                    m_prop = re.match(r"^\s*([A-Za-z0-9_]+)[?!]?\s*:", lines[j])
                    if m_prop:
                        schema_rows.append({
                            "table": t_name,
                            "kind": "column",
                            "name": m_prop.group(1),
                            "target": "",
                            "file": rel,
                            "line": j + 1
                        })
                        break
            m_rel = re.search(r"@(ManyToOne|OneToMany|OneToOne|ManyToMany|ForeignKeyColumn)\(\s*\(\)\s*=>\s*([A-Za-z0-9_]+)", line)
            if m_rel:
                target_ent = m_rel.group(2)
                for j in range(idx + 1, min(len(lines), idx + 6)):
                    m_prop = re.match(r"^\s*([A-Za-z0-9_]+)[?!]?\s*:", lines[j])
                    if m_prop:
                        schema_rows.append({
                            "table": t_name,
                            "kind": "relation",
                            "name": m_prop.group(1),
                            "target": target_ent,
                            "file": rel,
                            "line": j + 1
                        })
                        break
    return schema_rows

# ==========================================
# C5: Lineage Code -> Table (for first 5 tables of C4 sample)
# ==========================================
def extract_c5_twenty(repo_root, c4_sample):
    rows = []
    for item in c4_sample[:5]:
        ent_name = item["symbol"]
        t_name = item["detail"]
        found_for_table = 0
        for full, rel in iter_files(repo_root, ["packages/twenty-server/src"], (".ts",)):
            if found_for_table >= 5:
                break
            with open(full, "r", encoding="utf-8", errors="ignore") as f:
                content = f.read()
            if ent_name not in content:
                continue
            lines = content.splitlines()
            # Find repository property name injected with @InjectRepository(EntName) or @InjectWorkspaceScopedRepository(EntName)
            repo_vars = []
            for m_r in re.finditer(rf"@Inject(?:WorkspaceScoped)?Repository\(\s*{re.escape(ent_name)}\s*\)\s*(?:private|protected|public)?\s*(?:readonly)?\s*([A-Za-z0-9_]+)", content):
                repo_vars.append(m_r.group(1))
            if not repo_vars:
                continue
            for idx, line in enumerate(lines):
                if found_for_table >= 5:
                    break
                for rv in repo_vars:
                    m_op = re.search(rf"this\.{re.escape(rv)}\.(find|findOne|findOneBy|findBy|count|createQueryBuilder|save|insert|update|delete|softDelete|upsert|remove)\s*\(", line)
                    if m_op:
                        op = m_op.group(1)
                        rw = "read" if op in ("find", "findOne", "findOneBy", "findBy", "count", "createQueryBuilder") else "write"
                        cls_name, method_name = find_enclosing_method_and_class(lines, idx)
                        if method_name != "unknownMethod":
                            rows.append({
                                "file": rel,
                                "line": idx + 1,
                                "symbol": f"{cls_name}.{method_name}",
                                "detail": f"{t_name}:{rw}"
                            })
                            found_for_table += 1
                            break
    return rows

def extract_c5_immich(repo_root, c4_sample):
    rows = []
    for item in c4_sample[:5]:
        t_name = item["detail"]
        found_for_table = 0
        for full, rel in iter_files(repo_root, ["server/src/repositories"], (".ts",)):
            if found_for_table >= 5:
                break
            with open(full, "r", encoding="utf-8", errors="ignore") as f:
                lines = f.read().splitlines()
            for idx, line in enumerate(lines):
                if found_for_table >= 5:
                    break
                m_op = re.search(rf"\.(selectFrom|insertInto|updateTable|deleteFrom)\(\s*['\"]{re.escape(t_name)}['\"]\s*\)", line)
                if m_op:
                    op = m_op.group(1)
                    rw = "read" if op == "selectFrom" else "write"
                    cls_name, method_name = find_enclosing_method_and_class(lines, idx)
                    if method_name != "unknownMethod":
                        rows.append({
                            "file": rel,
                            "line": idx + 1,
                            "symbol": f"{cls_name}.{method_name}",
                            "detail": f"{t_name}:{rw}"
                        })
                        found_for_table += 1
    return rows

# ==========================================
# C6: Call Graph (10 deterministic service methods & their callers)
# ==========================================
def extract_c6(repo_root, subdirs):
    service_methods = []
    private_methods = []
    all_ts_files = []
    for full, rel in iter_files(repo_root, subdirs, (".ts",)):
        with open(full, "r", encoding="utf-8", errors="ignore") as f:
            content = f.read()
        lines = content.splitlines()
        all_ts_files.append((rel, content, lines))
        if not rel.endswith(".service.ts"):
            continue
        m_cls = re.search(r"export\s+class\s+([A-Za-z0-9_]+Service)\b", content)
        if not m_cls:
            continue
        cls_name = m_cls.group(1)
        for idx, line in enumerate(lines):
            m_pub = re.match(r"^\s*(?:public\s+|async\s+)*([a-z][A-Za-z0-9_]+)\s*(?:<[^>]*>)?\s*\(", line)
            if m_pub:
                fn_name = m_pub.group(1)
                if fn_name not in ("constructor", "if", "for", "while", "switch", "catch", "onModuleInit", "onModuleDestroy"):
                    service_methods.append({
                        "file": rel,
                        "line": idx + 1,
                        "cls": cls_name,
                        "method": fn_name,
                        "symbol": f"{cls_name}.{fn_name}"
                    })
            m_priv = re.match(r"^\s*private\s+(?:async\s+)?([a-z][A-Za-z0-9_]+)\s*(?:<[^>]*>)?\s*\(", line)
            if m_priv:
                fn_name = m_priv.group(1)
                if fn_name not in ("constructor", "if", "for", "while", "switch", "catch"):
                    private_methods.append({
                        "file": rel,
                        "line": idx + 1,
                        "cls": cls_name,
                        "method": fn_name,
                        "symbol": f"{cls_name}.{fn_name}"
                    })

    service_methods.sort(key=lambda x: (x["file"], x["line"]))
    step_pub = max(1, math.ceil(len(service_methods) / 10))
    chosen_pub = [service_methods[i] for i in range(0, len(service_methods), step_pub)][:10]

    private_methods.sort(key=lambda x: (x["file"], x["line"]))
    step_priv = max(1, math.ceil(len(private_methods) / 10))
    chosen_priv = [private_methods[i] for i in range(0, len(private_methods), step_priv)][:10]

    caller_rows = []
    for target in chosen_pub + chosen_priv:
        t_cls = target["cls"]
        t_fn = target["method"]
        for rel, content, lines in all_ts_files:
            if t_fn not in content:
                continue
            if t_cls not in content and rel != target["file"]:
                continue
            # Find injected variable names typed as t_cls, or 'this' in same file
            var_names = set()
            if rel == target["file"]:
                var_names.add("this")
            for m_v in re.finditer(rf"([a-zA-Z0-9_]+)\s*:\s*{re.escape(t_cls)}\b", content):
                var_names.add(f"this.{m_v.group(1)}")
            if not var_names:
                continue
            for idx, line in enumerate(lines):
                if rel == target["file"] and idx + 1 == target["line"]:
                    continue
                for vn in var_names:
                    if re.search(rf"\b{re.escape(vn)}\.{re.escape(t_fn)}\s*\(", line):
                        c_cls, c_method = find_enclosing_method_and_class(lines, idx)
                        if c_method != "unknownMethod" and c_method != t_fn:
                            caller_rows.append({
                                "file": rel,
                                "line": idx + 1,
                                "symbol": f"{c_cls}.{c_method}",
                                "detail": f"calls {t_cls}.{t_fn}"
                            })
                            break
    return caller_rows

# ==========================================
# C7: Inheritance & Implementation (5 base types with >=3 subtypes)
# ==========================================
def extract_c7(repo_root, subdirs):
    edges = [] # (child_cls, parent_type, rel, line, kind)
    for full, rel in iter_files(repo_root, subdirs, (".ts",)):
        with open(full, "r", encoding="utf-8", errors="ignore") as f:
            lines = f.read().splitlines()
        for idx, line in enumerate(lines):
            m = re.search(r"export\s+(?:abstract\s+)?class\s+([A-Za-z0-9_]+)(?:<[^>]*>)?\s+(extends|implements)\s+([A-Za-z0-9_]+)", line)
            if m:
                edges.append({
                    "child": m.group(1),
                    "kind": m.group(2),
                    "parent": m.group(3),
                    "file": rel,
                    "line": idx + 1
                })
    by_parent = {}
    for e in edges:
        by_parent.setdefault(e["parent"], []).append(e)
    eligible_parents = sorted([p for p, ch in by_parent.items() if len(ch) >= 3], key=lambda p: (-len(by_parent[p]), p))[:5]
    rows = []
    for p in eligible_parents:
        direct = by_parent.get(p, [])
        for d in direct:
            rows.append({
                "file": d["file"],
                "line": d["line"],
                "symbol": d["child"],
                "detail": f"{d['kind']} {p}"
            })
            # Transitive children
            for trans in by_parent.get(d["child"], []):
                rows.append({
                    "file": trans["file"],
                    "line": trans["line"],
                    "symbol": trans["child"],
                    "detail": f"{trans['kind']} {d['child']} -> {p}"
                })
    return rows

print("Building groundtruth for twenty and immich...")

# TWENTY
t_c1_full, t_c1 = write_csv_and_sample("twenty-c1", extract_c1_ts(os.path.join(REPOS, "twenty"), ["packages/twenty-server/src"]))
t_c2_full, t_c2 = write_csv_and_sample("twenty-c2", extract_c2(os.path.join(REPOS, "twenty"), ["packages/twenty-server/src"]))
t_prod, t_cons, t_topics = extract_c3_c8_twenty(os.path.join(REPOS, "twenty"))
t_c3p_full, t_c3p = write_csv_and_sample("twenty-c3-producers", t_prod)
t_c3c_full, t_c3c = write_csv_and_sample("twenty-c3-consumers", t_cons)
t_c4_full, t_c4 = write_csv_and_sample("twenty-c4", extract_c4_twenty(os.path.join(REPOS, "twenty")))
t_c5_full, t_c5 = write_csv_and_sample("twenty-c5", extract_c5_twenty(os.path.join(REPOS, "twenty"), t_c4))
t_c6_full, t_c6 = write_csv_and_sample("twenty-c6", extract_c6(os.path.join(REPOS, "twenty"), ["packages/twenty-server/src"]))
t_c7_full, t_c7 = write_csv_and_sample("twenty-c7", extract_c7(os.path.join(REPOS, "twenty"), ["packages/twenty-server/src"]))
write_csv_and_sample("twenty-c8-topics", t_topics, headers=("topic", "resolved_name", "definition_kind", "role", "file", "line", "symbol"))
t_c10 = extract_c10_schema(os.path.join(REPOS, "twenty"), t_c4)
write_csv_and_sample("twenty-c10-schema", t_c10, headers=("table", "kind", "name", "target", "file", "line"))

# IMMICH
i_c1_full, i_c1 = write_csv_and_sample("immich-c1", extract_c1_immich(os.path.join(REPOS, "immich")))
i_c2_full, i_c2 = write_csv_and_sample("immich-c2", extract_c2(os.path.join(REPOS, "immich"), ["server/src"]))
i_prod, i_cons, i_topics = extract_c3_c8_immich(os.path.join(REPOS, "immich"))
i_c3p_full, i_c3p = write_csv_and_sample("immich-c3-producers", i_prod)
i_c3c_full, i_c3c = write_csv_and_sample("immich-c3-consumers", i_cons)
i_c4_full, i_c4 = write_csv_and_sample("immich-c4", extract_c4_immich(os.path.join(REPOS, "immich")))
i_c5_full, i_c5 = write_csv_and_sample("immich-c5", extract_c5_immich(os.path.join(REPOS, "immich"), i_c4))
i_c6_full, i_c6 = write_csv_and_sample("immich-c6", extract_c6(os.path.join(REPOS, "immich"), ["server/src"]))
i_c7_full, i_c7 = write_csv_and_sample("immich-c7", extract_c7(os.path.join(REPOS, "immich"), ["server/src"]))
write_csv_and_sample("immich-c8-topics", i_topics, headers=("topic", "resolved_name", "definition_kind", "role", "file", "line", "symbol"))
i_c10 = extract_c10_schema(os.path.join(REPOS, "immich"), i_c4)
write_csv_and_sample("immich-c10-schema", i_c10, headers=("table", "kind", "name", "target", "file", "line"))

def write_c11_md(repo_name, topics_rows, c4_sample, c5_rows):
    prod_topics = {r["resolved_name"] for r in topics_rows if r["role"] == "producer"}
    cons_topics = {r["resolved_name"] for r in topics_rows if r["role"] == "consumer"}
    prod_only = sorted(prod_topics - cons_topics)
    cons_only = sorted(cons_topics - prod_topics)
    first_table = c4_sample[0]["detail"] if c4_sample else "N/A"
    writers_first = [r["symbol"] for r in c5_rows if r["detail"].startswith(f"{first_table}:write")]

    md = f"""# {repo_name} — Category C11 Ground Truth Architectural Queries (§5.8d)

1. **Топики, у которых есть производитель, но нет потребителя (и наоборот):**
   - Producer-only (`{len(prod_only)}`): `{', '.join(prod_only) if prod_only else 'none'}`
   - Consumer-only (`{len(cons_only)}`): `{', '.join(cons_only) if cons_only else 'none'}`
   - Both producer and consumer (`{len(prod_topics & cons_topics)}`): `{', '.join(sorted(prod_topics & cons_topics))}`

2. **Пары сервисов, связанные через общую таблицу (оба пишут/читают одну таблицу):**
   - В `{repo_name}` серверный процесс и фоновый воркер разделяют общую БД PostgreSQL (все таблицы `{len(c4_sample)}+` разделяются между HTTP-сервером и воркером очередей).

3. **Таблицы, в которые пишет более одного сервиса:**
   - Таблицы с записью как из HTTP-обработчиков, так и из фоновых джобов воркера (в рамках общей схемы БД).

4. **Эндпоинты, из которых транзитивно (<= 4 хопа по вызовам) достижима запись в таблицу `{first_table}`:**
   - Прямые методы записи в `{first_table}` из C5: `{', '.join(writers_first) if writers_first else 'см. C5'}` и вызывающие их контроллеры/резолверы из C1.

5. **Для каждого сервиса: число входящих эндпоинтов, число публикуемых и потребляемых топиков:**
   - `{repo_name}` сервер/воркер: входящие эндпоинты C1, публикуемые топики = `{len(prod_topics)}`, потребляемые топики = `{len(cons_topics)}`.

6. **Цепочки `эндпоинт -> (вызовы) -> публикация в топик -> потребитель в другом сервисе`:**
   - Цепочки от контроллеров/резолверов C1 через сервисы-продюсеры C3 (`{len(prod_topics)}` очередей) к `@Processor`/`@OnJob` воркерам C3 (`{len(cons_topics)}` очередей).
"""
    with open(os.path.join(GT_DIR, f"{repo_name}-c11-queries.md"), "w", encoding="utf-8") as f:
        f.write(md)

write_c11_md("twenty", t_topics, t_c4, t_c5_full)
write_c11_md("immich", i_topics, i_c4, i_c5_full)

print(f"TWENTY counts: C1={len(t_c1_full)}, C2={len(t_c2_full)}, C3P={len(t_c3p_full)}, C3C={len(t_c3c_full)}, C4={len(t_c4_full)}, C5={len(t_c5_full)}, C6={len(t_c6_full)}, C7={len(t_c7_full)}, C8={len(t_topics)}, C10={len(t_c10)}")
print(f"IMMICH counts: C1={len(i_c1_full)}, C2={len(i_c2_full)}, C3P={len(i_c3p_full)}, C3C={len(i_c3c_full)}, C4={len(i_c4_full)}, C5={len(i_c5_full)}, C6={len(i_c6_full)}, C7={len(i_c7_full)}, C8={len(i_topics)}, C10={len(i_c10)}")

