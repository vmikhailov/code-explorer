# GitHub Issue Triage Policy (Agy)

This document defines classification criteria (triage), decision rules, and response templates for the automated agent **Agy** in the [vmikhailov/code-explorer](https://github.com/vmikhailov/code-explorer) repository.

---

## 1. Classification Categories

Each newly opened issue is assigned to one of the following categories:

| Category | Agent Decision | GitHub Labels | Action |
| :--- | :--- | :--- | :--- |
| **Easy Bug** | Immediate auto-fix | `triaged`, `easy-bug`, `pr-available` | Creates an isolated Git Worktree, fixes the bug, runs tests, pushes PR, comments on the issue |
| **Medium / Hard Bug** | Analysis + tag author | `triaged`, `complex`, `needs-review` | Analyzes root cause, provides implementation plan and blast-radius evaluation, tags `@vmikhailov` |
| **Feature Request** | Architectural review | `triaged`, `enhancement`, `needs-review` | Evaluates alignment with graph ontology/design, proposes architecture, tags `@vmikhailov` |
| **Needs Repro** | Request details | `needs-repro` | Requests minimal reproduction repository or logs if information is insufficient for diagnosis |

---

## 2. "Easy Bug" Criteria (Automated Fix)

An issue is classified as an **Easy Bug** only if **all** of the following conditions are met:
1. **Locality**: The fix affects 1–2 files and does not exceed ~50–80 lines (typo, missing null check, incorrect operator, localized edge case in a parser or utility).
2. **Zero Breaking Changes**:
   - No modifications to public CLI commands, flags, or output formats.
   - No signature changes to public interfaces in `CodeExplorer.Core`.
   - No modifications to LSP protocol contracts or the extension-to-CLI communication contract.
3. **Graph & Ontology Schema Safety**:
   - No modifications to the SQLite schema (`graph.db`), node/edge type ontology, or Cypher compiler.
   - No modifications to Semantic Invariant Gate (SIG) rules.
4. **Deterministic Verification**:
   - Can be covered by an isolated unit test that reproduces the defect and validates the resolution.
   - `dotnet test cli/CodeExplorer.slnx -c Release` completes with **0 warnings and 0 errors**.

> [!CAUTION]
> If there is any ambiguity, risk of performance degradation, or need for refactoring, the issue **must be escalated to Medium / Hard Bug**. Automated fixes in such scenarios are strictly forbidden.

---

## 3. "Medium / Hard Bug" Criteria (Analysis Without Auto-Fix)

An issue is assigned to this category if:
- It affects cross-project dependencies (e.g., TypeScript parser + graph enrichment in Core + visualizer).
- It involves performance, parallelism (`Task`/`Parallel`), memory efficiency (`Span`/`Memory`), or AST traversal logic.
- It touches Cypher queries, graph storage schema, or blast-radius calculation.
- It requires choosing between alternative architectural designs.

**Agent Response Template:**
```markdown
Hi @vmikhailov,

I've triaged this issue. Here is my technical analysis and recommendation:

### Root Cause Analysis
<Detailed explanation of the root cause referencing specific classes and line numbers>

### Proposed Architecture & Solution
<Step-by-step implementation plan consistent with existing architecture>

### Potential Blast Radius & Risks
<Downstream components affected and required test suites>

— Agy
```

---

## 4. Conflict Prevention & Workspace Safety

1. **Working Tree Isolation (Git Worktree)**:
   - The agent **never modifies files or switches branches** in the main repository directory.
   - All fixes are performed exclusively in an isolated temporary worktree:
     ```powershell
     $wt = (pwsh -File scripts/github-triage.ps1 create-worktree -IssueNumber <id>).Trim()
     ```
   - After pushing the `fix/issue-<id>` branch, the worktree is cleanly removed:
     ```powershell
     pwsh -File scripts/github-triage.ps1 remove-worktree -IssueNumber <id>
     ```

2. **Active Task Exclusion**:
   - Issues with assigned contributors (`assignees.length > 0`) or labels `in-progress`, `wip`, `triaged` are **always skipped**.
   - Issues #3 and #4 are explicitly ignored (`IgnoreIssueNumbers`).
   - If a branch `fix/issue-<id>` already exists on origin, the issue is skipped to avoid stepping on active work.

3. **Mandatory Signature**:
   - All messages, comments, and Pull Request descriptions must conclude with:
     `— Agy`
