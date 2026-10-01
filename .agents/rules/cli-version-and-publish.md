---
trigger: always_on
---

# Versioning & Publishing Instructions (CLI & VS Code Extension)

Whenever fixes or features are made to CodeExplorer (CLI or VS Code Extension):

1. **Increment Version**:
   - Update `<Version>` in [cli/Directory.Build.props](file:///c:/Work/Personal/code-explorer/cli/Directory.Build.props) (e.g. from `1.17.0` to `1.18.0`). The assembly, file, informational, and NuGet package versions follow `$(Version)`.
   - Update `"version"` in [vscode-extension/package.json](file:///c:/Work/Personal/code-explorer/vscode-extension/package.json) to match.

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

