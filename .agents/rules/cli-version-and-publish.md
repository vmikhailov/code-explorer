---
trigger: always_on
---

# Versioning & Publishing Instructions (CLI & VS Code Extension)

Whenever fixes or features are made to CodeExplorer (CLI or VS Code Extension):

1. **Increment Version via SemVer (Skill: `versioning`)**:
   - Always follow the `versioning` skill ([.agents/skills/versioning/SKILL.md](file:///c:/Work/Personal/code-explorer/.agents/skills/versioning/SKILL.md)):
     - **Bug Fixes (PATCH)**: Increment only the last digit (`MAJOR.MINOR.(PATCH + 1)`). Example: `1.44.0` -> `1.44.1`.
     - **New Features / Functionality (MINOR)**: Increment the second digit (`MAJOR.(MINOR + 1).0`), resetting PATCH to `0`. Example: `1.44.0` -> `1.45.0`.
     - **Fundamental Paradigm Shift (MAJOR)**: Increment the first digit (`(MAJOR + 1).0.0`), resetting MINOR and PATCH to `0`. **STRICT REQUIREMENT**: Bumping MAJOR is forbidden without explicit prior user consent/approval.
   - Synchronize across all three version-bearing files:
     - `<Version>` in [cli/Directory.Build.props](file:///c:/Work/Personal/code-explorer/cli/Directory.Build.props).
     - `"version"` in [vscode-extension/package.json](file:///c:/Work/Personal/code-explorer/vscode-extension/package.json).
     - `"version"` in [vscode-extension/package-lock.json](file:///c:/Work/Personal/code-explorer/vscode-extension/package-lock.json).
   - Alternatively, execute the helper script:
     ```bash
     pwsh .agents/skills/versioning/scripts/bump-version.ps1 -Type patch # or -Type minor
     ```

2. **Verify Build and Test Suites**:
   - **CLI**:
     - Run `dotnet build cli/CodeExplorer.slnx -c Release` (compiles with 0 warnings and 0 errors; `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` is enforced globally via [Directory.Build.props](file:///c:/Work/Personal/code-explorer/cli/Directory.Build.props)).
     - Run `dotnet test cli/tests/CodeExplorer.Tests/CodeExplorer.Tests.csproj`.
     - Run `dotnet test cli/tests/CodeExplorer.Cypher.Tests/CodeExplorer.Cypher.Tests.csproj`.
   - **VS Code Extension**:
     - In `vscode-extension/`, run `npm run typecheck` and `npm run test`.

3. **Pack and Reinstall Local Tool (optional / for manual verification)**:
   - Pack the NuGet tool package:
     ```bash
     dotnet pack cli/src/UI/CodeExplorer/CodeExplorer.csproj -c Release -o ./cli/.Packages
     ```
   - Update the locally installed global tool:
     ```bash
     dotnet tool update -g codeexplorer.cli --add-source ./cli/.Packages
     ```
   - Package VSIX locally (optional):
     ```bash
     cd vscode-extension && npx @vscode/vsce package --no-dependencies
     ```

4. **Republish via Single Unified GitHub Tag**:
   - Both CLI and Extension are released together via a single unified workflow trigger:
     - Tag the release commit: `git tag v<NEW_VERSION>` (e.g. `git tag v1.18.0`).
     - Push commit and tag (`git push origin main --tags`).
   - The unified GitHub Actions workflow [release.yml](file:///c:/Work/Personal/code-explorer/.github/workflows/release.yml) automatically:
     - Runs CLI test suites and builds standalone native binaries for 6 architectures (Win/Linux/macOS x64 & ARM64).
     - Packages and publishes `CodeExplorer.Cli` to NuGet.org.
     - Typechecks, tests, packages the VS Code Extension (`.vsix`), attaches it to the GitHub Release, and publishes to VS Code Marketplace & Open VSX Registry.

5. **Post-Push GitHub Actions Verification (+10 Minutes Deferred Task)**:
   - During night or long-running task processing, after pushing commits/tags to GitHub, **ALWAYS schedule a deferred `+10 minutes` (`DurationSeconds: 600`) timer via the `schedule` tool** (`TimerCondition: "never"`) to check the GitHub Actions workflow status (`(Invoke-RestMethod -Uri "https://api.github.com/repos/vmikhailov/code-explorer/actions/runs?per_page=5").workflow_runs | Select-Object id, name, head_branch, status, conclusion`).
   - When the timer fires, verify that all CI/Release workflow jobs succeeded. If any job failed, automatically inspect the failure jobs/logs (`https://api.github.com/repos/vmikhailov/code-explorer/actions/runs/<run-id>/jobs`), fix the issue, increment the patch version, and re-verify until the GitHub Actions build is green.
