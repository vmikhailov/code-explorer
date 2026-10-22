param(
    [Parameter(Mandatory = $false)]
    [ValidateSet('patch', 'minor', 'major')]
    [string]$Type,

    [Parameter(Mandatory = $false)]
    [string]$TargetVersion,

    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..\..")).Path

$propsPath = Join-Path $repoRoot "cli\Directory.Build.props"
$pkgPath = Join-Path $repoRoot "vscode-extension\package.json"
$pkgLockPath = Join-Path $repoRoot "vscode-extension\package-lock.json"

if (-not (Test-Path $propsPath)) {
    throw "Directory.Build.props not found at: $propsPath"
}

# 1. Read current version from Directory.Build.props
$propsContent = Get-Content $propsPath -Raw
if ($propsContent -match '<Version Condition="''\$\(Version\)'' == ''''">(\d+)\.(\d+)\.(\d+)</Version>') {
    $currentMajor = [int]$Matches[1]
    $currentMinor = [int]$Matches[2]
    $currentPatch = [int]$Matches[3]
    $currentVersion = "$currentMajor.$currentMinor.$currentPatch"
} else {
    throw "Unable to parse current <Version> from $propsPath"
}

Write-Host "Current Version: $currentVersion" -ForegroundColor Cyan

# 2. Determine new version
if ($TargetVersion) {
    if ($TargetVersion -notmatch '^(\d+)\.(\d+)\.(\d+)$') {
        throw "TargetVersion '$TargetVersion' must follow SemVer (e.g. 1.44.1)"
    }
    $newVersion = $TargetVersion
} elseif ($Type) {
    switch ($Type) {
        'patch' {
            $newVersion = "$currentMajor.$currentMinor.$($currentPatch + 1)"
        }
        'minor' {
            $newVersion = "$currentMajor.$($currentMinor + 1).0"
        }
        'major' {
            if (-not $Force) {
                Write-Error "MAJOR version bump ($($currentMajor + 1).0.0) changes fundamental paradigms and requires explicit user consent! Pass -Force to proceed."
                exit 1
            }
            $newVersion = "$($currentMajor + 1).0.0"
        }
    }
} else {
    Write-Host "Usage:"
    Write-Host "  .\bump-version.ps1 -Type patch       # 1.44.0 -> 1.44.1 (Bug fixes / maintenance)"
    Write-Host "  .\bump-version.ps1 -Type minor       # 1.44.0 -> 1.45.0 (New features / functionality)"
    Write-Host "  .\bump-version.ps1 -Type major -Force # 1.44.0 -> 2.0.0 (Paradigm change, explicit consent required)"
    Write-Host "  .\bump-version.ps1 -TargetVersion 1.44.1"
    exit 0
}

Write-Host "Bumping to:      $newVersion ($Type)" -ForegroundColor Green

# 3. Update cli/Directory.Build.props
$newProps = $propsContent -replace '<Version Condition="''\$\(Version\)'' == ''''">(\d+)\.(\d+)\.(\d+)</Version>', ("<Version Condition=""'`$(Version)' == ''"">$newVersion</Version>")
Set-Content -Path $propsPath -Value $newProps -NoNewline
Write-Host "  [OK] Updated $propsPath" -ForegroundColor Gray

# 4. Update vscode-extension/package.json
if (Test-Path $pkgPath) {
    $pkgContent = Get-Content $pkgPath -Raw
    $newPkg = $pkgContent -replace '("version":\s*")\d+\.\d+\.\d+(")', ('${1}' + $newVersion + '${2}')
    Set-Content -Path $pkgPath -Value $newPkg -NoNewline
    Write-Host "  [OK] Updated $pkgPath" -ForegroundColor Gray
}

# 5. Update vscode-extension/package-lock.json
if (Test-Path $pkgLockPath) {
    $pkgLockContent = Get-Content $pkgLockPath -Raw
    # Update top-level version
    $newLock = $pkgLockContent -replace '("name":\s*"code-explorer-vscode",\s*"version":\s*")\d+\.\d+\.\d+(")', ('${1}' + $newVersion + '${2}')
    # Update packages[""].version
    $newLock = $newLock -replace '("":\s*\{\s*"name":\s*"code-explorer-vscode",\s*"version":\s*")\d+\.\d+\.\d+(")', ('${1}' + $newVersion + '${2}')
    Set-Content -Path $pkgLockPath -Value $newLock -NoNewline
    Write-Host "  [OK] Updated $pkgLockPath" -ForegroundColor Gray
}

# 6. Check & synchronize CHANGELOG.md -> vscode-extension/CHANGELOG.md
$rootChangelog = Join-Path $repoRoot "CHANGELOG.md"
$extChangelog = Join-Path $repoRoot "vscode-extension\CHANGELOG.md"
if (Test-Path $rootChangelog) {
    $changelogContent = Get-Content $rootChangelog -Raw
    if ($changelogContent -match [regex]::Escape("## [$newVersion]")) {
        Copy-Item -Path $rootChangelog -Destination $extChangelog -Force
        Write-Host "  [OK] Synchronized CHANGELOG.md -> vscode-extension/CHANGELOG.md (includes [$newVersion])" -ForegroundColor Gray
    } else {
        Write-Host "  [WARN] Root CHANGELOG.md does not yet contain '## [$newVersion]'. Update CHANGELOG.md and copy to vscode-extension/CHANGELOG.md before tagging!" -ForegroundColor Yellow
    }
}

Write-Host "`nVersion bumped successfully to $newVersion!" -ForegroundColor Green
Write-Host "Next steps:"
Write-Host "  1. Ensure ## [$newVersion] is documented in BOTH CHANGELOG.md and vscode-extension/CHANGELOG.md"
Write-Host "  2. dotnet build cli/CodeExplorer.slnx -c Release"
Write-Host "  3. dotnet test cli/tests/CodeExplorer.Tests/CodeExplorer.Tests.csproj"
Write-Host "  4. dotnet test cli/tests/CodeExplorer.Cypher.Tests/CodeExplorer.Cypher.Tests.csproj"
Write-Host "  5. cd vscode-extension; npm run typecheck; npm run test"
Write-Host "  6. git commit -am `"chore(release): v$newVersion`""
Write-Host "  7. git tag v$newVersion"
Write-Host "  8. git push origin main --tags"
