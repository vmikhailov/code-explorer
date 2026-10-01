# Strict Architectural Neutrality & No Hardcoding Rule

## 1. Absolute Ban on Project-Specific Knowledge in Engine Code
- **NEVER** embed, hardcode, or reference specific repository names, microservice names, table names, feature names, or custom internal functions in `cli/src/` (e.g. `ats`, `tbmap`, `domvain`, `approval`, `calc`, `settler`, `adhub`, `codeexplorer`, `getServiceDomainByRoute`, BigQuery dataset quirks).
- Any attempt to "fix" an analysis, categorization, or classification by adding keywords, prefixes, string patterns, or branches targeting a specific codebase directly into C# engine code is a **CRITICAL ARCHITECTURAL VIOLATION**.

## 2. Configuration-Driven Ingestion Only
- **ALL** repository-specific routing functions, custom prefixes, service-to-domain mappings, path globs/patterns, database aliases, and domain keyword dictionaries **MUST** be loaded dynamically from `.codeexplorer/conventions.json` or `.codeexplorer/domains.json` in the target workspace.
- If configuration files are missing, the engine **MUST** fall back to:
  1. Pure graph-topological clustering (Disjoint Set Union on shared database tables, event streams/topics, and direct service calls).
  2. Directory namespaces (e.g. `services/billing/*` -> `Billing`).
  3. Universal software engineering categories only: `UserInterface`, `SharedKernel`, `DeveloperTooling`, `TestingInfrastructure`.

## 3. Separation of Solution Space vs. Problem Space
- **Bounded Contexts (Solution Space):** Microservices, applications, workers, and gateways.
- **Business Domains (Problem Space):** High-level macro-domains configured by the user or synthesized from topological clusters.
- Database tables, message queues, and single microservices must **NEVER** become top-level domains.

## 4. Mandatory Pre-Commit Diff Verification
- Before completing any task, proposing changes, or making a Git commit, the agent **MUST** inspect `git diff` specifically for any domain-specific strings, hardcoded project names, or ad-hoc heuristics.
- If any project-specific string or bookmark was added to `cli/src/`, the edit must be rejected and reworked to use configuration or generic graph algorithms.
