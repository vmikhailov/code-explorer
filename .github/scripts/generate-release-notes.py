#!/usr/bin/env python3
"""
Generates rich GitHub Release notes from CHANGELOG.md for the current release tag.
Outputs to RELEASE_BODY.md for use with softprops/action-gh-release.
"""
import os
import re
import sys

def main():
    tag = sys.argv[1] if len(sys.argv) > 1 else os.environ.get("GITHUB_REF_NAME", "")
    version = tag.lstrip("v").strip()
    changelog_path = sys.argv[2] if len(sys.argv) > 2 else "CHANGELOG.md"
    output_path = sys.argv[3] if len(sys.argv) > 3 else "RELEASE_BODY.md"

    notes = ""
    if os.path.exists(changelog_path) and version:
        try:
            with open(changelog_path, "r", encoding="utf-8") as f:
                content = f.read()
            pattern = rf"## \[{re.escape(version)}\][^\n]*\n(.*?)(?=\n## \[|\Z)"
            match = re.search(pattern, content, re.DOTALL)
            if match:
                notes = match.group(1).strip()
                notes = re.sub(r"\n---\s*$", "", notes).strip()
        except Exception as e:
            print(f"::warning::Failed to parse CHANGELOG.md: {e}", file=sys.stderr)

    if not notes:
        notes = f"Release {tag}"

    footer = """

---

### 🚀 Install & Upgrade

**CLI (.NET Global Tool):**
```bash
dotnet tool update -g codeexplorer.cli
```

**VS Code Extension:**
Available on the [VS Code Marketplace](https://marketplace.visualstudio.com/items?itemName=vmikhailov.code-explorer-vscode) and [Open VSX Registry](https://open-vsx.org/extension/vmikhailov/code-explorer-vscode).

**Standalone Native Binaries:**
Pre-compiled self-contained single-file binaries are attached below for:
- **macOS:** Apple Silicon (`osx-arm64`), Intel (`osx-x64`)
- **Linux:** x64 (`linux-x64`), ARM64 (`linux-arm64`)
- **Windows:** x64 (`win-x64`), ARM64 (`win-arm64`)
"""

    with open(output_path, "w", encoding="utf-8") as out:
        out.write(notes + footer)
    print(f"Generated {output_path} ({len(notes + footer)} bytes)")

if __name__ == "__main__":
    main()
