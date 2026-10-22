---
name: versioning
description: Semantic Versioning (SemVer) and unified release lifecycle skill for CodeExplorer (CLI and VS Code Extension). Use whenever bumping versions, fixing bugs, releasing features, or preparing git release tags.
---

# Semantic Versioning (SemVer) & Release Lifecycle

This skill defines the strict versioning and release lifecycle for CodeExplorer (CLI and VS Code Extension).

Both the CLI and VS Code Extension share a **single, unified version number** and are released together via a unified GitHub Actions workflow.

---

## 1. SemVer Version Increment Rules

Versions strictly follow the format: `MAJOR.MINOR.PATCH` (e.g. `1.44.0`).

### 1.1. Patch Version (`MAJOR.MINOR.PATCH` -> `MAJOR.MINOR.(PATCH + 1)`)
- **When to use**:
  - Fixing bugs or crashes.
  - Fixing regressions or edge-case parsing anomalies.
  - Improving error handling, diagnostics, or logging.
  - Internal refactoring that does not change external APIs, CLI commands, or UI behaviors.
  - Maintenance, dependency bumps without breaking changes, or test suite additions.
- **Action**: Increment ONLY the last digit (`PATCH`). `MAJOR` and `MINOR` remain unchanged.
- **Example**: `1.44.0` -> `1.44.1`.

### 1.2. Minor Version (`MAJOR.MINOR.PATCH` -> `MAJOR.(MINOR + 1).0`)
- **When to use**:
  - Adding new user-facing functionality or features.
  - Adding new CLI verbs or commands (e.g., `ce test`, `ce coverage`).
  - Adding new MCP tools or extending tool parameters.
  - Adding new language parsers or syntax extractors.
  - Adding new UI views, panels, graph layouts, or filters in the VS Code extension.
  - Backward-compatible architectural enhancements.
- **Action**: Increment the middle digit (`MINOR`) and reset `PATCH` to `0`.
- **Example**: `1.44.0` -> `1.45.0`.

### 1.3. Major Version (`MAJOR.0.0` -> `(MAJOR + 1).0.0`)
- **When to use**:
  - Fundamental paradigm shift in architecture or execution model.
  - Breaking changes to public APIs, CLI flags/output schemas, or MCP tool contracts.
  - Complete overhaul of the graph ontology schema requiring migration.
- **CRITICAL CONSTRAINT**:
  > [!IMPORTANT]
  > **MAJOR version increments are STRICTLY PROHIBITED without explicit prior user consent/approval.**
  > The agent must NEVER bump the MAJOR version autonomously. Always propose the major bump to the user, explain why the paradigm shift requires it, and wait for explicit confirmation before changing the major version.
- **Action** (upon user approval): Increment the first digit (`MAJOR`), reset `MINOR` to `0`, and reset `PATCH` to `0`.
- **Example**: `1.44.0` -> `2.0.0`.

---

## 2. Synchronized Version & Changelog Files

When updating the version, all of the following files **must** be updated simultaneously:

1. **`cli/Directory.Build.props`**:
   ```xml
   <!-- Versioning -->
   <Version Condition="'$(Version)' == ''">MAJOR.MINOR.PATCH</Version>
   ```
   *(All assembly, file, informational, and NuGet package versions automatically inherit `$(Version)`)*.

2. **`vscode-extension/package.json`**:
   ```json
   "version": "MAJOR.MINOR.PATCH"
   ```

3. **`vscode-extension/package-lock.json`**:
   - Update `"version"` at the top level.
   - Update `"version"` inside `packages[""]`.

4. **`CHANGELOG.md` AND `vscode-extension/CHANGELOG.md` (Mandatory Dual-Changelog Rule)**:
   - Every release **must** document its changes under `## [MAJOR.MINOR.PATCH] - YYYY-MM-DD` in **both** root [`CHANGELOG.md`](file:///c:/Work/Personal/code-explorer/CHANGELOG.md) and [`vscode-extension/CHANGELOG.md`](file:///c:/Work/Personal/code-explorer/vscode-extension/CHANGELOG.md).
   - Because `vsce package` bundles `vscode-extension/CHANGELOG.md` into the VS Code Marketplace & Open VSX extension page, `vscode-extension/CHANGELOG.md` must never lag behind root `CHANGELOG.md`.
   - When proposals in `docs/proposals/` or plans in `docs/roadmap/` are 100% completed as part of a release, fold their delivered summary into the changelog and remove the completed proposal file.

---

## 3. Automation Helper Script

Use the provided PowerShell script in this skill to safely bump all files in one step:

```powershell
# For bug fixes (bumps PATCH):
pwsh .agents/skills/versioning/scripts/bump-version.ps1 -Type patch

# For new features / functionality (bumps MINOR, resets PATCH to 0):
pwsh .agents/skills/versioning/scripts/bump-version.ps1 -Type minor

# For fundamental paradigm change (requires user consent, pass -Force):
pwsh .agents/skills/versioning/scripts/bump-version.ps1 -Type major -Force

# Or specify exact version:
pwsh .agents/skills/versioning/scripts/bump-version.ps1 -TargetVersion 1.44.1
```

---

## 4. Verification Checklist (Prior to Tagging & Release)

Before committing and tagging a new version, run the full verification suite:

### 4.1. CLI Verification
- **Build**:
  ```bash
  dotnet build cli/CodeExplorer.slnx -c Release
  ```
  *(Must compile with **0 warnings** and **0 errors**; `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` is enforced globally)*.
- **Core Tests**:
  ```bash
  dotnet test cli/tests/CodeExplorer.Tests/CodeExplorer.Tests.csproj
  ```
- **Cypher Engine Tests**:
  ```bash
  dotnet test cli/tests/CodeExplorer.Cypher.Tests/CodeExplorer.Cypher.Tests.csproj
  ```

### 4.2. VS Code Extension Verification
- **Typecheck & Tests**:
  ```bash
  cd vscode-extension
  npm run typecheck
  npm run test
  ```

---

## 5. Release & Tagging Protocol

Once verified:

1. **Commit Changes**:
   Use Conventional Commits:
   - For bug fixes: `fix: <description>` or `chore(release): vX.Y.Z`
   - For features: `feat: <description>` or `chore(release): vX.Y.Z`
   - Example:
     ```bash
     git commit -am "chore(release): v1.44.1"
     ```

2. **Create Git Tag**:
   The tag **must** have the format `v<MAJOR>.<MINOR>.<PATCH>`:
   ```bash
   git tag v1.44.1
   ```

3. **Push to Remote**:
   ```bash
   git push origin main --tags
   ```

4. **CI/CD Automation (Unified Tag Release)**:
   The unified GitHub Actions workflow ([`.github/workflows/release.yml`](file:///c:/Work/Personal/code-explorer/.github/workflows/release.yml)) is triggered strictly by the tag:
   - Builds native single-file binaries for **win-x64** and **osx-arm64** (reduced native matrix).
   - Packs and publishes universal `codeexplorer.cli` NuGet tool package to NuGet.org and GitHub Releases (serving non-native architectures: `linux-*`, `osx-x64`, `win-arm64`).
   - Packages lightweight universal `.vsix` extension and publishes to VS Code Marketplace & Open VSX Registry.
   - **Important**: Do **NOT** schedule any timer/hook or background check to monitor GitHub Actions after pushing the tag. The user monitors GitHub Actions manually.



