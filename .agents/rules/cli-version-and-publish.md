---
trigger: always_on
---

# Versioning & Publishing Instructions (CLI & VS Code Extension)

Whenever fixes or features are made to CodeExplorer (CLI or VS Code Extension):

1. **Increment Version via SemVer (Skill: `versioning`)**:
   - Always follow the `versioning` skill ([.agents/skills/versioning/SKILL.md](../../.agents/skills/versioning/SKILL.md)):
     - **Bug Fixes (PATCH)**: Increment only the last digit (`MAJOR.MINOR.(PATCH + 1)`). Example: `1.44.0` -> `1.44.1`.
     - **New Features / Functionality (MINOR)**: Increment the second digit (`MAJOR.(MINOR + 1).0`), resetting PATCH to `0`. Example: `1.44.0` -> `1.45.0`.
     - **Fundamental Paradigm Shift (MAJOR)**: Increment the first digit (`(MAJOR + 1).0.0`), resetting MINOR and PATCH to `0`. **STRICT REQUIREMENT**: Bumping MAJOR is forbidden without explicit prior user consent/approval.
   - Synchronize across all version-bearing and changelog files:
     - `<Version>` in [cli/Directory.Build.props](../../cli/Directory.Build.props).
     - `"version"` in [vscode-extension/package.json](../../vscode-extension/package.json).
     - `"version"` in [vscode-extension/package-lock.json](../../vscode-extension/package-lock.json).
     - **Both Changelogs (Mandatory)**: Always add the new `[X.Y.Z] - YYYY-MM-DD` release section to **both** [CHANGELOG.md](../../CHANGELOG.md) and [vscode-extension/CHANGELOG.md](../../vscode-extension/CHANGELOG.md) (keep them identical so the VS Code Marketplace extension changelog never lags behind).
   - Alternatively, execute the helper script (which bumps versions and syncs `CHANGELOG.md` $\to$ `vscode-extension/CHANGELOG.md`):
     ```bash
     pwsh .agents/skills/versioning/scripts/bump-version.ps1 -Type patch # or -Type minor
     ```

2. **Verify Build and Test Suites**:
   - **CLI**:
     - Run `dotnet build cli/CodeExplorer.slnx -c Release` (compiles with 0 warnings and 0 errors; `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` is enforced globally via [Directory.Build.props](../../cli/Directory.Build.props)).
     - Run `dotnet test cli/tests/CodeExplorer.Tests/CodeExplorer.Tests.csproj`.
     - Run `dotnet test cli/tests/CodeExplorer.Cypher.Tests/CodeExplorer.Cypher.Tests.csproj`.
   - **VS Code Extension**:
     - In `vscode-extension/`, run `npm run typecheck` and `npm run test`.

3. **Pack and Reinstall Local Tool (optional / for manual verification)**:
   - **One-step Local Redeployment (Recommended)**:
     Handles background process file-locking (e.g. `ce.exe serve` spawned by VS Code), packs, uninstalls old version, and installs fresh binaries:
     ```bash
     .\update-local.cmd # or: pwsh scripts/update-local.ps1
     ```
     To optionally increment patch version before packing:
     ```bash
     pwsh scripts/update-local.ps1 -BumpPatch
     ```
   - Alternatively, manual pack and update:
     ```bash
     dotnet pack cli/src/UI/CodeExplorer/CodeExplorer.csproj -c Release -o ./cli/.Packages
     dotnet tool update -g codeexplorer.cli --add-source ./cli/.Packages
     ```
   - Package VSIX locally (optional):
     ```bash
     cd vscode-extension && npx @vscode/vsce package --no-dependencies
     ```

4. **Republish via Single Unified GitHub Tag**:
   - Both CLI and Extension are released together via a single unified release workflow triggered strictly by a tag:
     - Tag the release commit: `git tag v<NEW_VERSION>` (e.g. `git tag v1.18.0`).
     - Push commit and tag (`git push origin main --tags`).
   - The unified GitHub Actions workflow ([`release.yml`](../../.github/workflows/release.yml)) automatically:
     - Builds native standalone binaries for **win-x64** and **osx-arm64** (reduced native matrix).
     - Packs and publishes universal `codeexplorer.cli` NuGet tool package to NuGet.org and GitHub Releases (serving non-native architectures: `linux-*`, `osx-x64`, `win-arm64`).
     - Typechecks, tests, packages the lightweight universal VS Code Extension (`.vsix`), attaches it to the GitHub Release, and publishes to VS Code Marketplace & Open VSX Registry.
   - **Do NOT schedule any timer/hook or poll GitHub Actions** after pushing the release tag (the user checks CI/CD status manually).



