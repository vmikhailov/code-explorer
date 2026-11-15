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

$coreRepo     = "https://github.com/tree-sitter/tree-sitter.git"
$coreCommit   = "afcfe343ea050771f3deb59ab788c27882418bbc"

$csharpRepo   = "https://github.com/vmikhailov/tree-sitter-c-sharp.git"
$csharpCommit = "374984463f73e12fc5bc0326651d1fcca61e48fb"

$tsRepo       = "https://github.com/vmikhailov/tree-sitter-typescript.git"
$tsCommit     = "e660d046fcbc92427ed5fba098fed002a58f642a"

$jsRepo       = "https://github.com/vmikhailov/tree-sitter-javascript.git"
$jsCommit     = "1f0ac36f28d33be17c1f39bc5f265730d146b525"

$goRepo       = "https://github.com/vmikhailov/tree-sitter-go.git"
$goCommit     = "b07b8a1baa93183b36db54b7f363bb0d46205145"

$pyRepo       = "https://github.com/tree-sitter/tree-sitter-python.git"
$pyCommit     = "bffb65a8cfe4e46290331dfef0dbf0ef3679de11"

$javaRepo     = "https://github.com/tree-sitter/tree-sitter-java.git"
$javaCommit   = "94703d5a6bed02b98e438d7cad1136c01a60ba2c"

$rustRepo     = "https://github.com/tree-sitter/tree-sitter-rust.git"
$rustCommit   = "77a3747266f4d621d0757825e6b11edcbf991ca5"

$rRepo        = "https://github.com/r-lib/tree-sitter-r.git"
$rCommit      = "58a22794466c0fc15b0d3b40531db751593721e8"

$zigVersion   = "0.13.0"

$scriptDir    = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot     = (Resolve-Path (Join-Path $scriptDir "..")).Path
$buildRoot    = Join-Path $repoRoot "cli/.Build"
$workDir      = Join-Path $buildRoot "tree-sitter-native"
$runtimesDir  = Join-Path $buildRoot "runtimes"
$stampFile    = Join-Path $runtimesDir ".stamp"
$expectedStamp = "core=$coreCommit;csharp=$csharpCommit;ts=$tsCommit;js=$jsCommit;go=$goCommit;py=$pyCommit;java=$javaCommit;rust=$rustCommit;r=$rCommit;rids=$($Rids -join ',')"

$expectedLibs = @(
    "tree-sitter",
    "tree-sitter-c-sharp",
    "tree-sitter-typescript",
    "tree-sitter-tsx",
    "tree-sitter-javascript",
    "tree-sitter-go",
    "tree-sitter-python",
    "tree-sitter-java",
    "tree-sitter-rust",
    "tree-sitter-r"
)

# 1. Check if already built and up-to-date
if (-not $Force -and (Test-Path $stampFile)) {
    $actualStamp = (Get-Content $stampFile -Raw).Trim()
    $allExist = $true
    foreach ($rid in $Rids) {
        $ext = if ($rid.StartsWith("win-")) { ".dll" } elseif ($rid.StartsWith("osx-")) { ".dylib" } else { ".so" }
        $prefix = if ($rid.StartsWith("win-")) { "" } else { "lib" }
        foreach ($g in $expectedLibs) {
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
function Ensure-GrammarRepo([string]$Name, [string]$RepoUrl, [string]$CommitSha) {
    $dest = Join-Path $workDir "src/$Name"
    $headFile = Join-Path $dest ".commit"
    if ((Test-Path $headFile) -and ((Get-Content $headFile -Raw).Trim() -eq $CommitSha)) {
        return $dest
    }

    if (Test-Path $dest) {
        Remove-Item $dest -Recurse -Force
    }
    New-Item -ItemType Directory -Force $dest | Out-Null

    Write-Host "[tree-sitter-native] Fetching $Name @ $CommitSha from $RepoUrl ..." -ForegroundColor Cyan
    & git init --quiet $dest
    & git -C $dest remote add origin $RepoUrl
    & git -C $dest fetch --quiet --depth 1 origin $CommitSha
    & git -C $dest checkout --quiet FETCH_HEAD
    Set-Content $headFile $CommitSha -NoNewline
    return $dest
}

$coreSrc = Ensure-GrammarRepo -Name "tree-sitter"            -RepoUrl $coreRepo   -CommitSha $coreCommit
$csSrc   = Ensure-GrammarRepo -Name "tree-sitter-c-sharp"    -RepoUrl $csharpRepo -CommitSha $csharpCommit
$tsSrc   = Ensure-GrammarRepo -Name "tree-sitter-typescript" -RepoUrl $tsRepo     -CommitSha $tsCommit
$jsSrc   = Ensure-GrammarRepo -Name "tree-sitter-javascript" -RepoUrl $jsRepo     -CommitSha $jsCommit
$goSrc   = Ensure-GrammarRepo -Name "tree-sitter-go"         -RepoUrl $goRepo     -CommitSha $goCommit
$pySrc   = Ensure-GrammarRepo -Name "tree-sitter-python"     -RepoUrl $pyRepo     -CommitSha $pyCommit
$javaSrc = Ensure-GrammarRepo -Name "tree-sitter-java"       -RepoUrl $javaRepo   -CommitSha $javaCommit
$rustSrc = Ensure-GrammarRepo -Name "tree-sitter-rust"       -RepoUrl $rustRepo   -CommitSha $rustCommit
$rSrc    = Ensure-GrammarRepo -Name "tree-sitter-r"          -RepoUrl $rRepo      -CommitSha $rCommit

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
        Name    = "tree-sitter"
        WorkDir = $coreSrc
        Include = @("-I", (Join-Path $coreSrc "lib/include"), "-I", (Join-Path $coreSrc "lib/src"))
        Sources = @((Join-Path $coreSrc "lib/src/lib.c"))
    },
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
    },
    @{
        Name    = "tree-sitter-javascript"
        WorkDir = $jsSrc
        Include = @("-I", (Join-Path $jsSrc "src"))
        Sources = @((Join-Path $jsSrc "src/parser.c"), (Join-Path $jsSrc "src/scanner.c"))
    },
    @{
        Name    = "tree-sitter-go"
        WorkDir = $goSrc
        Include = @("-I", (Join-Path $goSrc "src"))
        Sources = @((Join-Path $goSrc "src/parser.c"))
    },
    @{
        Name    = "tree-sitter-python"
        WorkDir = $pySrc
        Include = @("-I", (Join-Path $pySrc "src"))
        Sources = @((Join-Path $pySrc "src/parser.c"), (Join-Path $pySrc "src/scanner.c"))
    },
    @{
        Name    = "tree-sitter-java"
        WorkDir = $javaSrc
        Include = @("-I", (Join-Path $javaSrc "src"))
        Sources = @((Join-Path $javaSrc "src/parser.c"))
    },
    @{
        Name    = "tree-sitter-rust"
        WorkDir = $rustSrc
        Include = @("-I", (Join-Path $rustSrc "src"))
        Sources = @((Join-Path $rustSrc "src/parser.c"), (Join-Path $rustSrc "src/scanner.c"))
    },
    @{
        Name    = "tree-sitter-r"
        WorkDir = $rSrc
        Include = @("-I", (Join-Path $rSrc "src"))
        Sources = @((Join-Path $rSrc "src/parser.c"), (Join-Path $rSrc "src/scanner.c"))
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
