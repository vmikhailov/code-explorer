[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "",
    [switch]$All,
    [string]$OutputDir = "",
    [switch]$NoCompression
)

$ErrorActionPreference = "Stop"

$ScriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
if (-not $ScriptDir) { $ScriptDir = Join-Path (Get-Location) "scripts" }
$SolutionDir = (Resolve-Path "$ScriptDir/..").Path
$Project = Join-Path $SolutionDir "src/UI/CodeExplorer/CodeExplorer.csproj"

$BaseOutputDir = if ($OutputDir) {
    if ([System.IO.Path]::IsPathRooted($OutputDir)) { $OutputDir } else { Join-Path $SolutionDir $OutputDir }
} else {
    Join-Path $SolutionDir ".Build\bin"
}

$AllRuntimes = @("win-x64", "linux-x64", "linux-arm64", "osx-arm64", "osx-x64")

if ($All) {
    $TargetRuntimes = $AllRuntimes
} elseif ($Runtime) {
    $TargetRuntimes = @($Runtime)
} else {
    # Default to current OS / win-x64
    if ([System.OperatingSystem]::IsLinux()) {
        $TargetRuntimes = @("linux-x64")
    } elseif ([System.OperatingSystem]::IsMacOS()) {
        $arch = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString().ToLowerInvariant()
        $TargetRuntimes = if ($arch -eq "arm64") { @("osx-arm64") } else { @("osx-x64") }
    } else {
        $TargetRuntimes = @("win-x64")
    }
}

$compression = if ($NoCompression) { "false" } else { "true" }

Write-Host ""
Write-Host "=== CodeExplorer ('ce') Single-File Publisher ===" -ForegroundColor Cyan
Write-Host "  Configuration: $Configuration"
Write-Host "  Targets:       $($TargetRuntimes -join ', ')"
Write-Host "  Base Output:   $BaseOutputDir"
Write-Host ""

$results = @()

foreach ($target in $TargetRuntimes) {
    $targetDir = if ($All) { Join-Path $BaseOutputDir $target } else { $BaseOutputDir }

    if (-not (Test-Path $targetDir)) {
        New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    }

    $resolvedTargetDir = (Resolve-Path -Path $targetDir).Path

    Write-Host "Publishing single-file self-contained binary for '$target'..." -ForegroundColor Yellow

    $dotnetArgs = @(
        "publish", $Project,
        "-c", $Configuration,
        "-r", $target,
        "--self-contained", "true",
        "-p:PublishSingleFile=true",
        "-p:IncludeNativeLibrariesForSelfExtract=true",
        "-p:IncludeAllContentForSelfExtract=true",
        "-p:EnableCompressionInSingleFile=$compression",
        "-p:PublishTrimmed=false",
        "-p:DebugType=none",
        "-o", $resolvedTargetDir
    )

    & dotnet $dotnetArgs

    if ($LASTEXITCODE -ne 0) {
        Write-Host "[FAIL] Publish failed for $target (exit code $LASTEXITCODE)" -ForegroundColor Red
        exit $LASTEXITCODE
    }

    $binFile = Get-ChildItem -Path $resolvedTargetDir -File | Where-Object { $_.Name -eq "ce.exe" -or $_.Name -eq "ce" } | Select-Object -First 1
    if ($binFile) {
        $sizeMb = [math]::Round($binFile.Length / (1024 * 1024), 2)
        $results += [PSCustomObject]@{
            Platform = $target
            Binary   = $binFile.Name
            SizeMB   = $sizeMb
            Path     = $binFile.FullName
        }
    }
}

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "[OK] Successfully published all requested single-file targets:" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
foreach ($res in $results) {
    $summaryLine = "  [{0}] {1} ({2} MB) -> {3}" -f $res.Platform, $res.Binary, $res.SizeMB, $res.Path
    Write-Host $summaryLine -ForegroundColor Gray
}
Write-Host ""
