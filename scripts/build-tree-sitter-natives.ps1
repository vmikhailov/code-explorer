<#
.SYNOPSIS
    Fetches and cross-compiles custom ABI-14 Tree-sitter native grammars (c-sharp, typescript, tsx)
    on the fly into cli/.Build/runtimes/<rid>/native/ using Zig CC.

.DESCRIPTION
    All intermediate sources, toolchains, and output binaries are stored strictly under cli/.Build/.
    If 'zig' is not found on PATH, a portable Zig toolchain (0.13.0) is automatically downloaded
    and cached in cli/.Build/tools/zig/.

.PARAMETER Rids
    Target Runtime Identifiers to build. Defaults to all 6 supported RIDs:
    win-x64, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64.

.PARAMETER Force
    Forces recompilation even if cli/.Build/runtimes/.stamp is up to date.
#>

[CmdletBinding()]
param(
    [string[]]$Rids = @("win-x64", "win-arm64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64"),
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$csharpRepo   = "https://github.com/vmikhailov/tree-sitter-c-sharp.git"
$csharpCommit = "3e84193374546752812dec17a06ac00eef21703d"

$tsRepo       = "https://github.com/vmikhailov/tree-sitter-typescript.git"
$tsCommit     = "8bed1df777b440b029da3230f48799c431eabf6b"

$zigVersion   = "0.13.0"

$scriptDir    = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot     = (Resolve-Path (Join-Path $scriptDir "..")).Path
$buildRoot    = Join-Path $repoRoot "cli/.Build"
$workDir      = Join-Path $buildRoot "tree-sitter-native"
$runtimesDir  = Join-Path $buildRoot "runtimes"
$stampFile    = Join-Path $runtimesDir ".stamp"
$expectedStamp = "csharp=$csharpCommit;ts=$tsCommit;rids=$($Rids -join ',')"

# 1. Check if already built and up-to-date
if (-not $Force -and (Test-Path $stampFile)) {
    $actualStamp = (Get-Content $stampFile -Raw).Trim()
    $allExist = $true
    foreach ($rid in $Rids) {
        $ext = if ($rid.StartsWith("win-")) { ".dll" } elseif ($rid.StartsWith("osx-")) { ".dylib" } else { ".so" }
        $prefix = if ($rid.StartsWith("win-")) { "" } else { "lib" }
        foreach ($g in @("tree-sitter-c-sharp", "tree-sitter-typescript", "tree-sitter-tsx")) {
            $binPath = Join-Path $runtimesDir "$rid/native/$prefix$g$ext"
            if (-not (Test-Path $binPath)) {
                $allExist = $false
                break
            }
        }
    }
    if ($allExist -and $actualStamp -eq $expectedStamp) {
        Write-Host "[tree-sitter-native] Up to date in cli/.Build/runtimes ($expectedStamp)" -ForegroundColor DarkGray
        exit 0
    }
}

New-Item -ItemType Directory -Force $workDir | Out-Null
New-Item -ItemType Directory -Force $runtimesDir | Out-Null

# 2. Resolve or download portable Zig into cli/.Build/tools/zig
function Resolve-ZigExe {
    $cmd = Get-Command zig -ErrorAction SilentlyContinue
    if ($cmd) {
        return $cmd.Source
    }

    $toolsDir = Join-Path $buildRoot "tools/zig"
    $isWin = $IsWindows -or ($env:OS -eq "Windows_NT")
    $isMac = $IsMacOS
    $arch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
    $zigArch = if ($arch -eq "arm64" -or $arch -eq "aarch64") { "aarch64" } else { "x86_64" }

    if ($isWin) {
        $zigPlatform = "windows"
        $archiveExt = "zip"
        $exeName = "zig.exe"
    } elseif ($isMac) {
        $zigPlatform = "macos"
        $archiveExt = "tar.xz"
        $exeName = "zig"
    } else {
        $zigPlatform = "linux"
        $archiveExt = "tar.xz"
        $exeName = "zig"
    }

    $folderName = "zig-$zigPlatform-$zigArch-$zigVersion"
    $zigExe = Join-Path $toolsDir "$folderName/$exeName"
    if (Test-Path $zigExe) {
        return $zigExe
    }

    New-Item -ItemType Directory -Force $toolsDir | Out-Null
    $url = "https://ziglang.org/download/$zigVersion/$folderName.$archiveExt"
    $archivePath = Join-Path $toolsDir "$folderName.$archiveExt"
    Write-Host "[tree-sitter-native] Downloading portable Zig $zigVersion from $url ..." -ForegroundColor Cyan
    Invoke-WebRequest -Uri $url -OutFile $archivePath

    if ($archiveExt -eq "zip") {
        Expand-Archive -Path $archivePath -DestinationPath $toolsDir -Force
    } else {
        & tar -xf $archivePath -C $toolsDir
    }
    Remove-Item $archivePath -Force -ErrorAction SilentlyContinue
    return $zigExe
}

$zigExe = Resolve-ZigExe
Write-Host "[tree-sitter-native] Using Zig: $zigExe" -ForegroundColor Cyan

# 3. Fetch pinned C sources into cli/.Build/tree-sitter-native/src
function Ensure-GrammarRepo([string]$Name, [string]$RepoUrl, [string]$CommitSha, [string]$LocalFallback) {
    $dest = Join-Path $workDir "src/$Name"
    $headFile = Join-Path $dest ".commit"
    if ((Test-Path $headFile) -and ((Get-Content $headFile -Raw).Trim() -eq $CommitSha)) {
        return $dest
    }

    if (Test-Path $dest) {
        Remove-Item $dest -Recurse -Force
    }
    New-Item -ItemType Directory -Force $dest | Out-Null

    if ($LocalFallback -and (Test-Path (Join-Path $LocalFallback ".git"))) {
        $localHead = (& git -C $LocalFallback rev-parse HEAD 2>$null).Trim()
        if ($localHead -eq $CommitSha) {
            Write-Host "[tree-sitter-native] Copying $Name @ $CommitSha from local checkout $LocalFallback ..." -ForegroundColor Cyan
            & git clone --quiet --no-checkout $LocalFallback $dest
            & git -C $dest checkout --quiet $CommitSha
            Set-Content $headFile $CommitSha -NoNewline
            return $dest
        }
    }

    Write-Host "[tree-sitter-native] Fetching $Name @ $CommitSha from $RepoUrl ..." -ForegroundColor Cyan
    & git init --quiet $dest
    & git -C $dest remote add origin $RepoUrl
    & git -C $dest fetch --quiet --depth 1 origin $CommitSha
    & git -C $dest checkout --quiet FETCH_HEAD
    Set-Content $headFile $CommitSha -NoNewline
    return $dest
}

$csSrc = Ensure-GrammarRepo -Name "tree-sitter-c-sharp" -RepoUrl $csharpRepo -CommitSha $csharpCommit -LocalFallback "C:/Work/Github/tree-sitter-c-sharp"
$tsSrc = Ensure-GrammarRepo -Name "tree-sitter-typescript" -RepoUrl $tsRepo -CommitSha $tsCommit -LocalFallback "C:/Work/Github/tree-sitter-typescript"

# 4. Target mapping for Zig CC
$targetMap = @{
    "win-x64"     = @{ Target = "x86_64-windows-gnu";       Prefix = "";    Ext = ".dll"   }
    "win-arm64"   = @{ Target = "aarch64-windows-gnu";      Prefix = "";    Ext = ".dll"   }
    "linux-x64"   = @{ Target = "x86_64-linux-gnu.2.17";    Prefix = "lib"; Ext = ".so"    }
    "linux-arm64" = @{ Target = "aarch64-linux-gnu.2.17";   Prefix = "lib"; Ext = ".so"    }
    "osx-x64"     = @{ Target = "x86_64-macos.11.0";        Prefix = "lib"; Ext = ".dylib" }
    "osx-arm64"   = @{ Target = "aarch64-macos.11.0";       Prefix = "lib"; Ext = ".dylib" }
}

$grammars = @(
    @{
        Name    = "tree-sitter-c-sharp"
        WorkDir = $csSrc
        Include = @("-I", (Join-Path $csSrc "src"))
        Sources = @((Join-Path $csSrc "src/parser.c"), (Join-Path $csSrc "src/scanner.c"))
    },
    @{
        Name    = "tree-sitter-typescript"
        WorkDir = (Join-Path $tsSrc "typescript")
        Include = @("-I", (Join-Path $tsSrc "typescript/src"), "-I", (Join-Path $tsSrc "common"))
        Sources = @((Join-Path $tsSrc "typescript/src/parser.c"), (Join-Path $tsSrc "typescript/src/scanner.c"))
    },
    @{
        Name    = "tree-sitter-tsx"
        WorkDir = (Join-Path $tsSrc "tsx")
        Include = @("-I", (Join-Path $tsSrc "tsx/src"), "-I", (Join-Path $tsSrc "common"))
        Sources = @((Join-Path $tsSrc "tsx/src/parser.c"), (Join-Path $tsSrc "tsx/src/scanner.c"))
    }
)

$zigCacheDir = Join-Path $workDir "zig-cache"
$env:ZIG_LOCAL_CACHE_DIR = $zigCacheDir
$env:ZIG_GLOBAL_CACHE_DIR = $zigCacheDir

foreach ($rid in $Rids) {
    if (-not $targetMap.ContainsKey($rid)) {
        throw "Unsupported RID '$rid'."
    }
    $cfg = $targetMap[$rid]
    $outNativeDir = Join-Path $runtimesDir "$rid/native"
    New-Item -ItemType Directory -Force $outNativeDir | Out-Null

    foreach ($g in $grammars) {
        $outFile = Join-Path $outNativeDir "$($cfg.Prefix)$($g.Name)$($cfg.Ext)"
        Write-Host "[tree-sitter-native] Compiling $($g.Name) for $rid ($($cfg.Target))..." -ForegroundColor Green

        $argsList = @(
            "cc",
            "-shared",
            "-O2",
            "-DNDEBUG",
            "-fno-sanitize=undefined",
            "-s",
            "-target", $cfg.Target
        ) + $g.Include + $g.Sources + @("-o", $outFile)

        & $zigExe $argsList
        if ($LASTEXITCODE -ne 0) {
            throw "zig cc failed for $($g.Name) ($rid) with exit code $LASTEXITCODE"
        }
    }
    Get-ChildItem -Path $outNativeDir -Include *.lib,*.pdb -File -Recurse -ErrorAction SilentlyContinue | Remove-Item -Force
}

Set-Content $stampFile $expectedStamp -NoNewline
Write-Host "[tree-sitter-native] Successfully built native grammars into $runtimesDir" -ForegroundColor Green
