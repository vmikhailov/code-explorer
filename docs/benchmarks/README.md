# CodeExplorer Benchmarking & Evaluation Suites 🧪

This directory contains curated benchmark registries used for automated testing, parser verification, and architectural stress tests across **CodeExplorer (`ce`)**.

---

## 📊 Benchmark Suites Overview

CodeExplorer benchmarks codebases across 5 core languages (**C#**, **TypeScript**, **Go**, **Java**, **Python**) divided into two distinct testing tiers:

| Benchmark Tier | Catalog Document | Target Size | Star Range | Purpose & Testing Focus |
| :--- | :--- | :---: | :---: | :--- |
| **Flagship Tier** | [**`flagship-projects.md`**](./flagship-projects.md) | Large Monorepos / Apps | 14k – 204k ⭐ | Stress-testing indexing scalability, deep inheritance hierarchies, multi-service topologies, and SQLite CTE limits. |
| **Compact Tier** | [**`compact-projects.md`**](./compact-projects.md) | Micro-Libraries / Tools (~1k–25k LOC) | 2k – 61k ⭐ | Rapid automated cloning (1–5s), immediate AST parsing, syntax parser debugging, and tight agent iteration feedback loops. |

---

## 🎯 How AI Agents Use These Benchmarks

When improving CodeExplorer's parsers, Cypher compiler, or intent distillation:

1. **Fast Prototyping (Compact Tier)**:
   - Clone any repository from [**`compact-projects.md`**](./compact-projects.md) using shallow clone (`git clone --depth 1 <URL>`).
   - Run `ce scan` and inspect diagnostic logs.
   - Verify syntactic symbols, call chains (`ce call-chain`), and endpoints (`ce endpoints`).
   - Iterate and tweak Tree-sitter or Roslyn visitor grammars with sub-second re-index runs.

2. **Enterprise Scale & Stress Testing (Flagship Tier)**:
   - Clone repositories from [**`flagship-projects.md`**](./flagship-projects.md) (e.g., `aspnetcore`, `vscode`, `supabase`).
   - Test memory usage, batch transaction performance (`DatabasePersistenceWriter`), and recursive Cypher query bounds.
   - Validate C1 System Context and C2 Service Flow layout stability in the webview cockpit.

---

## 📂 Catalog Files

- [**`flagship-projects.md`**](./flagship-projects.md): 50 high-star open-source flagship projects across C#, TypeScript, Go, Java, and Python.
- [**`compact-projects.md`**](./compact-projects.md): 50 compact, clean, and highly idiomatic libraries across all 5 supported languages.
