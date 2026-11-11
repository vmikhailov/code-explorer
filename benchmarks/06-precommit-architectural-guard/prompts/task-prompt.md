# Shared Task Prompt: Automated Pre-Commit Architectural Guard Audit on `argoproj/argo-cd`

You are acting as an automated Pre-Commit / CI Pull-Request Architectural Guard on `argoproj/argo-cd`.

Before any new code can be merged into `main`, it must pass an exhaustive verification of the following 6 core architectural invariant rules:

1. **Rule 1 (Reverse Layer Leaks)**: Foundational utility packages (`util/**`, excluding `*_test.go`) must remain self-contained and must never call code in higher-level business services (`server/**`, `controller/**`, `reposerver/**`).
2. **Rule 2 (God Structs & Complexity Limit)**: No production struct may exceed 30 declared methods.
3. **Rule 3 (Package Dependency DAG)**: Package import relationships across the repository must form a strictly acyclic directed graph (zero import cycles).
4. **Rule 4 (Storage Abstraction Boundary)**: Raw Kubernetes secret repository storage backends (`repository_secrets.go`) must be encapsulated strictly within `util/db` and never called from other packages.
5. **Rule 5 (Notification Subsystem Decoupling)**: Notification utilities in `util/notification` must not instantiate standalone database instances or call `db.NewDB` directly (DB instances must be injected).
6. **Rule 6 (Public Authentication Bypass Audit)**: Identify all server endpoints that implement `AuthFuncOverride` to bypass standard authentication.

Your task is to evaluate each of the 6 rules across the entire repository and produce a comprehensive Pre-Commit Architectural Guard Report:
For each rule, report:
- Rule status: `PASS` / `FAIL` / `WARNING`
- Total count of violations
- Concrete list of violations with exact file paths, line numbers, caller symbols, and target symbols
- Remediation guidance for any failing rules

Deliver your final audit report via `send_message`.
