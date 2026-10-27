<#
.SYNOPSIS
    Builds, packs, and updates the local global 'codeexplorer.cli' ('ce') tool.

.DESCRIPTION
    Handles Windows file-locking gracefully by stopping any background 'ce' daemon processes
    (e.g., spawned by the VS Code extension) during the tool update window, then verifies installation.

.PARAMETER Configuration
    Build configuration: 'Release' (default) or 'Debug'.

.PARAMETER BumpPatch
    Automatically increments the PATCH version in Directory.Build.props before packing.

.EXAMPLE
    pwsh scripts/update-local.ps1
    pwsh scripts/update-local.ps1 -BumpPatch
#>

[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [switch]$BumpPatch
)

$ErrorActionPreference = "Stop"

# 1. Resolve workspace root
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = (Resolve-Path (Join-Path $scriptDir "..")).Path
$propsFile = Join-Path $rootDir "cli\Directory.Build.props"
$cliProj = Join-Path $rootDir "cli\src\UI\CodeExplorer\CodeExplorer.csproj"
$packagesDir = Join-Path $rootDir "cli\.Packages"

Write-Host "=== CodeExplorer Local Tool Updater ===" -ForegroundColor Cyan
Write-Host "Workspace: $rootDir"

# 2. Optional: Bump patch version
if ($BumpPatch) {
    if (Test-Path $propsFile) {
        $content = Get-Content $propsFile -Raw
        if ($content -match '<Version[^>]*>(\d+)\.(\d+)\.(\d+)</Version>') {
            $major = [int]$matches[1]
            $minor = [int]$matches[2]
            $patch = [int]$matches[3] + 1
            $newVer = "$major.$minor.$patch"
            $content = $content -replace '<Version[^>]*>\d+\.\d+\.\d+</Version>', "<Version Condition=`"'\`$(Version)' == ''`">$newVer</Version>"
            Set-Content $propsFile $content -NoNewline
            Write-Host "Bumped version to $newVer in $propsFile" -ForegroundColor Yellow
        }
    }
}

# 3. Pack the NuGet tool package
Write-Host "`nPacking codeexplorer.cli ($Configuration)..." -ForegroundColor Cyan
if (-not (Test-Path $packagesDir)) {
    New-Item -ItemType Directory -Path $packagesDir -Force | Out-Null
}

$packArgs = @("pack", $cliProj, "-c", $Configuration, "-o", $packagesDir, "--nologo")
& dotnet $packArgs
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: dotnet pack failed with exit code $LASTEXITCODE." -ForegroundColor Red
    exit $LASTEXITCODE
}

# 4. Reinstall the global tool with background killer to prevent file locks
Write-Host "`nReinstalling global 'codeexplorer.cli' tool..." -ForegroundColor Cyan

$killer = Start-Job -ScriptBlock {
    while ($true) {
        Get-Process ce -ErrorAction SilentlyContinue | Stop-Process -Force
        Start-Sleep -Milliseconds 100
    }
}

try {
    # Uninstall first to ensure fresh binaries are deployed even if version didn't change
    & dotnet tool uninstall -g codeexplorer.cli 2>$null
    & dotnet tool install -g codeexplorer.cli --add-source $packagesDir --no-cache
} finally {
    Stop-Job $killer -ErrorAction SilentlyContinue
    Remove-Job $killer -ErrorAction SilentlyContinue
}

if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: dotnet tool update failed." -ForegroundColor Red
    exit $LASTEXITCODE
}

# 5. Verify installed version
Write-Host "`nVerification:" -ForegroundColor Cyan
try {
    $verOutput = & ce --version
    Write-Host "Installed: $verOutput" -ForegroundColor Green
} catch {
    Write-Host "Warning: Could not invoke 'ce --version'. You may need to restart your terminal or verify %USERPROFILE%\.dotnet\tools is on your PATH." -ForegroundColor Yellow
}

Write-Host "`n✓ Local tool redeployed successfully!" -ForegroundColor Green

