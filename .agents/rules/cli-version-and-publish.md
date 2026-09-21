---
trigger: always_on
---

# CLI Versioning & Publishing Instructions

Whenever fixes or features are made to the CLI (`cli/` codebase):

1. **Increment Version**:
   - Update `<Version>` in [cli/Directory.Build.props](file:///c:/Work/Personal/code-explorer/cli/Directory.Build.props) (e.g. from `1.4.0` to `1.4.1`).
   - The assembly, file, informational, and package versions will automatically follow `$(Version)`.

2. **Verify Build and Test Suites**:
   - Run `dotnet build cli/CodeExplorer.slnx -c Release /warnaserror` (must compile with 0 warnings and 0 errors).
   - Run `dotnet test cli/tests/CodeExplorer.Tests/CodeExplorer.Tests.csproj`.
   - Run `dotnet test cli/tests/CodeExplorer.Cypher.Tests/CodeExplorer.Cypher.Tests.csproj`.

3. **Pack and Reinstall Local Tool**:
   - Pack the NuGet tool package:
     ```bash
     dotnet pack cli/src/UI/CodeExplorer/CodeExplorer.csproj -c Release -o ./cli/.Packages
     ```
   - Update the locally installed global tool:
     ```bash
     dotnet tool update -g codeexplorer.cli --add-source ./cli/.Packages
     ```

4. **Republish via GitHub Release / NuGet**:
   - Tag the release commit: `git tag v<NEW_VERSION>` (e.g. `git tag v1.4.1`).
   - Push commit and tag (`git push origin main --tags`) to trigger the GitHub Action [release.yml](file:///c:/Work/Personal/code-explorer/.github/workflows/release.yml) which builds single-file binaries and publishes to NuGet.org.
