[CmdletBinding()]
param(
    [string]$Language = "",
    [int]$Limit = 0,
    [int]$Offset = 0,
    [string]$RepoName = "",
    [string]$ReposFile = "c:\Work\Personal\code-explorer\scripts\multilang-repositories.json",
    [string]$TestingDir = "C:\Work\Personal\code-explorer-testing",
    [string]$CeDll = "c:\Work\Personal\code-explorer\cli\.Build\bin_Release_AnyCPU\CodeExplorer\ce.dll",
    [int]$ScanTimeoutSeconds = 120,
    [switch]$ForceRescan,
    [switch]$KeepClone
)

$ErrorActionPreference = "Continue"

if (-not (Test-Path $TestingDir)) {
    New-Item -ItemType Directory -Path $TestingDir -Force | Out-Null
}

$resultsJsonPath = Join-Path $TestingDir "results.json"
$reportMdPath = Join-Path $TestingDir "OVERNIGHT_REPORT.md"

$allResults = @{}
if (Test-Path $resultsJsonPath) {
    try {
        $raw = Get-Content $resultsJsonPath -Raw
        $parsed = ConvertFrom-Json $raw -AsHashtable
        if ($parsed) { $allResults = $parsed }
    } catch {
        Write-Warning "Could not parse existing results.json: $_"
    }
}

$UtilityIgnorePrefixes = @(
    "java.", "javax.annotation", "jakarta.annotation", "jakarta.xml.bind", "javax.xml.bind", "jakarta.servlet", "javax.servlet", "jakarta.validation", "javax.validation",
    "kotlin.", "kotlinx.datetime", "kotlinx.coroutines", "org.jetbrains.kotlin", "org.jetbrains.kotlinx:kotlinx-coroutines", "org.jspecify",
    "org.slf4j", "ch.qos.logback", "org.apache.logging", "org.projectlombok", "lombok",
    "com.google.guava", "org.apache.commons", "commons-", "com.fasterxml.jackson", "tools.jackson", "com.google.code.gson",
    "org.jetbrains.annotations", "org.checkerframework", "com.google.errorprone", "org.yaml:snakeyaml", "org.javassist", "com.google.protobuf", "com.squareup.okio", "org.agrona",
    "org.apache.maven.plugins", "org.codehaus.mojo", "org.jacoco", "org.graalvm.buildtools", "io.github.git-commit-id", "com.gitlab.haynes", "org.apache.rat", "org.eclipse.jkube", "com.mycila", "org.webjars", "org.openapitools:jackson-databind-nullable", "io.vavr",
    "org.junit", "junit:", "org.mockito", "org.assertj", "org.hamcrest", "org.awaitility", "de.flapdoodle.embed", "com.vaadin.external.google:android-json",
    "system.", "microsoft.extensions.logging", "microsoft.extensions.options", "microsoft.extensions.primitives", "microsoft.extensions.configuration", "microsoft.extensions.dependencyinjection",
    "microsoft.csharp", "microsoft.net.test.sdk", "coverlet.", "newtonsoft.json", "humanizer", "bogus",
    "lodash", "ramda", "date-fns", "dayjs", "moment", "clsx", "classnames", "tailwind",
    "chalk", "commander", "yargs", "dotenv", "tslib", "zod", "valibot", "yup", "uuid", "nanoid",
    "typing", "dataclasses", "pathlib", "datetime", "collections", "itertools", "functools",
    "rich", "click", "typer", "colorama", "tqdm", "python-dotenv", "packaging", "setuptools",
    "github.com/spf13/cobra", "github.com/spf13/pflag", "github.com/spf13/viper",
    "github.com/pkg/errors", "github.com/google/uuid", "golang.org/x/sys", "golang.org/x/text", "golang.org/x/sync", "golang.org/x/crypto", "golang.org/x/net", "golang.org/x/time",
    "clap", "anyhow", "thiserror", "once_cell", "lazy_static", "regex", "chrono", "time", "uuid", "rand", "itertools", "bytes", "log", "env_logger"
)

function Get-RegisteredPatterns {
    param([string]$ParsersRoot = "c:\Work\Personal\code-explorer\cli\src\Parsers")
    $map = @{}
    $langs = @("Java", "Kotlin", "CSharp", "TypeScript", "Python", "Go", "Rust")
    foreach ($l in $langs) {
        $dir = Join-Path $ParsersRoot "CodeExplorer.Parsers.$l\Libraries"
        $patterns = [System.Collections.Generic.List[string]]::new()
        if (Test-Path $dir) {
            Get-ChildItem -Path $dir -Filter "*LibraryParser.cs" | ForEach-Object {
                $text = Get-Content $_.FullName -Raw
                $mList = [regex]::Matches($text, '"([^"]+)"')
                foreach ($m in $mList) {
                    $val = $m.Groups[1].Value
                    if ($val.Length -ge 2 -and $val -notmatch '\s') {
                        $patterns.Add($val.ToLowerInvariant())
                    }
                }
            }
        }
        $map[$l] = $patterns
    }
    return $map
}

$RegisteredPatternsByLang = Get-RegisteredPatterns

function Test-LibraryCovered {
    param([string]$Lang, [string]$Pkg)
    $pLower = $Pkg.ToLowerInvariant().Trim()
    if ([string]::IsNullOrWhiteSpace($pLower) -or $pLower.Length -lt 3) { return $true }
    foreach ($ignore in $UtilityIgnorePrefixes) {
        if ($pLower.StartsWith($ignore.ToLowerInvariant())) { return $true }
    }
    $patterns = $RegisteredPatternsByLang[$Lang]
    if (-not $patterns) { return $false }
    foreach ($pat in $patterns) {
        $cleanPat = $pat.Replace("*", "")
        if ($cleanPat.Length -ge 3 -and ($pLower.Contains($cleanPat) -or $cleanPat.Contains($pLower))) {
            return $true
        }
    }
    return $false
}

function Extract-ManifestDependencies {
    param([string]$Lang, [string]$RepoDir)
    $deps = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    try {
        switch ($Lang) {
            { $_ -in @("Java", "Kotlin") } {
                Get-ChildItem -Path $RepoDir -Include "pom.xml", "build.gradle", "build.gradle.kts", "libs.versions.toml" -Recurse -Depth 3 -ErrorAction SilentlyContinue | ForEach-Object {
                    $txt = Get-Content $_.FullName -Raw -ErrorAction SilentlyContinue
                    if ($_.Name -eq "pom.xml") {
                        foreach ($m in [regex]::Matches($txt, '<groupId>([^<$]+)</groupId>\s*<artifactId>([^<$]+)</artifactId>')) {
                            [void]$deps.Add("$($m.Groups[1].Value.Trim()):$($m.Groups[2].Value.Trim())")
                        }
                    } else {
                        foreach ($m in [regex]::Matches($txt, '(?:implementation|api|compileOnly|runtimeOnly|ksp|kapt)\s*\(?\s*["'']([a-zA-Z0-9_.\-]+:[a-zA-Z0-9_.\-]+)')) {
                            [void]$deps.Add($m.Groups[1].Value.Trim())
                        }
                    }
                }
            }
            "CSharp" {
                Get-ChildItem -Path $RepoDir -Include "*.csproj", "Directory.Packages.props" -Recurse -Depth 4 -ErrorAction SilentlyContinue | ForEach-Object {
                    $txt = Get-Content $_.FullName -Raw -ErrorAction SilentlyContinue
                    foreach ($m in [regex]::Matches($txt, '<PackageReference\s+Include="([^"]+)"')) {
                        [void]$deps.Add($m.Groups[1].Value.Trim())
                    }
                }
            }
            "TypeScript" {
                Get-ChildItem -Path $RepoDir -Filter "package.json" -Recurse -Depth 3 -ErrorAction SilentlyContinue | Where-Object { $_.FullName -notmatch 'node_modules' } | ForEach-Object {
                    try {
                        $pkgJson = Get-Content $_.FullName -Raw | ConvertFrom-Json
                        if ($pkgJson.dependencies) {
                            $pkgJson.dependencies.PSObject.Properties.Name | ForEach-Object { [void]$deps.Add($_) }
                        }
                    } catch {}
                }
            }
            "Python" {
                Get-ChildItem -Path $RepoDir -Include "pyproject.toml", "requirements*.txt" -Recurse -Depth 3 -ErrorAction SilentlyContinue | ForEach-Object {
                    $txt = Get-Content $_.FullName -Raw -ErrorAction SilentlyContinue
                    foreach ($m in [regex]::Matches($txt, '(?m)^\s*["'']?([a-zA-Z0-9_\-]+)\s*(?:[><=~!;\[]|$)')) {
                        $val = $m.Groups[1].Value.Trim()
                        if ($val -notin @("name", "version", "description", "python", "readme", "license", "authors", "dependencies", "optional", "requires")) {
                            [void]$deps.Add($val)
                        }
                    }
                }
            }
            "Go" {
                Get-ChildItem -Path $RepoDir -Filter "go.mod" -Recurse -Depth 3 -ErrorAction SilentlyContinue | ForEach-Object {
                    $txt = Get-Content $_.FullName -Raw -ErrorAction SilentlyContinue
                    foreach ($m in [regex]::Matches($txt, '(?m)^\s*([a-zA-Z0-9_.\-]+\.[a-zA-Z0-9_.\-/]+)\s+v[0-9]')) {
                        [void]$deps.Add($m.Groups[1].Value.Trim())
                    }
                }
            }
            "Rust" {
                Get-ChildItem -Path $RepoDir -Filter "Cargo.toml" -Recurse -Depth 3 -ErrorAction SilentlyContinue | ForEach-Object {
                    $txt = Get-Content $_.FullName -Raw -ErrorAction SilentlyContinue
                    foreach ($m in [regex]::Matches($txt, '(?m)^([a-zA-Z0-9_\-]+)\s*=\s*(?:["'']|\{)')) {
                        $val = $m.Groups[1].Value.Trim()
                        if ($val -notin @("name", "version", "edition", "authors", "description", "license", "repository", "homepage", "documentation", "readme", "keywords", "categories", "workspace", "members", "resolver", "path")) {
                            [void]$deps.Add($val)
                        }
                    }
                }
            }
        }
    } catch {}
    return @($deps)
}

function Run-CeScalarQuery {
    param([string]$TargetDir, [string]$Query)
    try {
        $out = & dotnet $CeDll query --dir $TargetDir $Query 2>&1 | Out-String
        if ($out -match '(?:cnt:\s*|-\s*|\|\s*)(\d+)') {
            return [int]$matches[1]
        }
    } catch {}
    return 0
}

function Update-ReportMarkdown {
    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.AppendLine("# CodeExplorer 700-Repository Multi-Language Library & Parser Validation Report")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("| # | Lang | Repository | Files | Nodes | Rels | Endpoints | Tables | Topics | ExtSvc | Scan (s) | Issues | Uncovered Deps | Status |")
    [void]$sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|")

    $idx = 1
    foreach ($entry in ($allResults.Values | Sort-Object { [string]$_.timestamp })) {
        $filesStr = if ($null -ne $entry.files) { $entry.files } else { "-" }
        $nodesStr = if ($null -ne $entry.nodes) { "{0:N0}" -f $entry.nodes } else { "-" }
        $relsStr = if ($null -ne $entry.relationships) { "{0:N0}" -f $entry.relationships } else { "-" }
        $epStr = if ($null -ne $entry.endpoints) { $entry.endpoints } else { "0" }
        $tblStr = if ($null -ne $entry.tables) { $entry.tables } else { "0" }
        $topStr = if ($null -ne $entry.topics) { $entry.topics } else { "0" }
        $extStr = if ($null -ne $entry.external_services) { $entry.external_services } else { "0" }
        $timeStr = if ($entry.scan_seconds) { "$($entry.scan_seconds)" } else { "-" }
        $issuesStr = if ($entry.issues -gt 0) { "**$($entry.issues)**" } else { "0" }
        $uncovCount = if ($entry.uncovered_deps) { @($entry.uncovered_deps).Count } else { 0 }
        $statusIcon = if ($entry.status -eq "SUCCESS") { "PASSED" } else { "**FAILED**" }

        [void]$sb.AppendLine("| $idx | $($entry.lang) | [$($entry.repo)](https://github.com/$($entry.repo)) | $filesStr | $nodesStr | $relsStr | $epStr | $tblStr | $topStr | $extStr | $timeStr | $issuesStr | $uncovCount | $statusIcon |")
        $idx++
    }

    $sb.ToString() | Set-Content $reportMdPath -Encoding UTF8
}

$reposJson = Get-Content $ReposFile -Raw | ConvertFrom-Json
if (-not [string]::IsNullOrEmpty($RepoName)) {
    $reposJson = @($reposJson | Where-Object { $_.name -eq $RepoName -or $_.repo -eq $RepoName })
} else {
    if (-not [string]::IsNullOrEmpty($Language)) {
        $reposJson = @($reposJson | Where-Object { $_.lang -eq $Language })
    }
    if ($Offset -gt 0) {
        $reposJson = @($reposJson | Select-Object -Skip $Offset)
    }
    if ($Limit -gt 0) {
        $reposJson = @($reposJson | Select-Object -First $Limit)
    }
}

$total = $reposJson.Count
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " CodeExplorer 700-Repo Library Expansion & Validation" -ForegroundColor Cyan
Write-Host " Selected Repositories: $total" -ForegroundColor Cyan
Write-Host " Testing Base Dir:      $TestingDir" -ForegroundColor Cyan
Write-Host " CE Binary:             $CeDll" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$currentIdx = 0
foreach ($item in $reposJson) {
    $currentIdx++
    $lang = $item.lang
    $repo = $item.repo
    $name = $item.name
    $targetDir = Join-Path $TestingDir $name

    Write-Host ""
    Write-Host "[$currentIdx/$total] Processing [$lang] $repo ($name)..." -ForegroundColor Green

    if ($allResults.ContainsKey($name) -and -not $ForceRescan) {
        $existing = $allResults[$name]
        if ($existing.status -eq "SUCCESS") {
            Write-Host "  -> Already verified ($($existing.nodes) nodes, $($existing.relationships) rels). Skipping." -ForegroundColor DarkGray
            continue
        }
    }

    try {
        # 1. Shallow clone (--depth 1 --filter=blob:none)
        if (Test-Path $targetDir) {
            Remove-Item -Recurse -Force $targetDir -ErrorAction SilentlyContinue
        }
        Write-Host "  [Git] Shallow cloning https://github.com/$repo.git..." -ForegroundColor Gray
        $cloneStart = Get-Date
        $env:GIT_TERMINAL_PROMPT = "0"
        & git clone --depth 1 --filter=blob:none --single-branch "https://github.com/$repo.git" $targetDir 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "  [Git Error] Failed to clone $repo"
            $allResults[$name] = @{
                lang = $lang
                repo = $repo
                name = $name
                status = "CLONE_FAILED"
                error = "Git clone failed"
                timestamp = (Get-Date).ToString("o")
            }
            $allResults | ConvertTo-Json -Depth 6 | Set-Content $resultsJsonPath -Encoding UTF8
            Update-ReportMarkdown
            continue
        }
        $cloneDuration = [math]::Round(((Get-Date) - $cloneStart).TotalSeconds, 1)
        Write-Host "  [Git] Cloned in ${cloneDuration}s" -ForegroundColor Gray

        # 2. Extract manifest dependencies & check uncovered libraries
        $manifestDeps = Extract-ManifestDependencies -Lang $lang -RepoDir $targetDir
        $uncoveredDeps = @($manifestDeps | Where-Object { -not (Test-LibraryCovered -Lang $lang -Pkg $_) })

        # 3. Init & Scan (with hard per-repo timeout watchdog)
        & dotnet $CeDll init --dir $targetDir | Out-Null
        Write-Host "  [CE] Scanning workspace (timeout ${ScanTimeoutSeconds}s)..." -ForegroundColor Yellow
        $scanStart = Get-Date
        $stdoutLog = Join-Path $TestingDir "$name.scan.out.log"
        $stderrLog = Join-Path $TestingDir "$name.scan.err.log"
        $scanProc = Start-Process -FilePath "dotnet" -ArgumentList @($CeDll, "scan", "--clear", $targetDir) -NoNewWindow -PassThru -RedirectStandardOutput $stdoutLog -RedirectStandardError $stderrLog
        $completedInTime = $scanProc.WaitForExit($ScanTimeoutSeconds * 1000)
        $scanDuration = [math]::Round(((Get-Date) - $scanStart).TotalSeconds, 1)

        if (-not $completedInTime) {
            Write-Warning "  [CE TIMEOUT] Scan exceeded ${ScanTimeoutSeconds}s on $repo (PID $($scanProc.Id)). Killing process..."
            Stop-Process -Id $scanProc.Id -Force -ErrorAction SilentlyContinue
            $scanExitCode = 124
            $scanOutput = "Scan timed out after ${ScanTimeoutSeconds}s"
        } else {
            $scanExitCode = $scanProc.ExitCode
            $outText = if (Test-Path $stdoutLog) { Get-Content $stdoutLog -Raw -ErrorAction SilentlyContinue } else { "" }
            $errText = if (Test-Path $stderrLog) { Get-Content $stderrLog -Raw -ErrorAction SilentlyContinue } else { "" }
            $scanOutput = "$outText`n$errText"
        }
        Remove-Item $stdoutLog, $stderrLog -Force -ErrorAction SilentlyContinue

        if ($scanExitCode -ne 0) {
            Write-Host "  [CE FAILED] Scan exited with code $scanExitCode in ${scanDuration}s" -ForegroundColor Red
            $allResults[$name] = @{
                lang = $lang
                repo = $repo
                name = $name
                status = "SCAN_FAILED"
                scan_seconds = $scanDuration
                manifest_deps = $manifestDeps
                uncovered_deps = $uncoveredDeps
                error = ($scanOutput | Out-String).Trim()
                timestamp = (Get-Date).ToString("o")
            }
            $allResults | ConvertTo-Json -Depth 6 | Set-Content $resultsJsonPath -Encoding UTF8
            Update-ReportMarkdown
            continue
        }

        # 4. Extract Graph & Semantic Yield Telemetry
        $nodeCount = Run-CeScalarQuery -TargetDir $targetDir -Query "MATCH (n) RETURN count(n) AS cnt"
        $relCount  = Run-CeScalarQuery -TargetDir $targetDir -Query "MATCH ()-[r]->() RETURN count(r) AS cnt"
        $fileCount = Run-CeScalarQuery -TargetDir $targetDir -Query "MATCH (f:File) RETURN count(f) AS cnt"
        $issueCount = Run-CeScalarQuery -TargetDir $targetDir -Query "MATCH (i:ScanIssue) RETURN count(i) AS cnt"
        $epCount   = Run-CeScalarQuery -TargetDir $targetDir -Query "MATCH (e:Endpoint) RETURN count(e) AS cnt"
        $tblCount  = Run-CeScalarQuery -TargetDir $targetDir -Query "MATCH (t:Table) RETURN count(t) AS cnt"
        $topCount  = Run-CeScalarQuery -TargetDir $targetDir -Query "MATCH (t:Topic) RETURN count(t) AS cnt"
        $extCount  = Run-CeScalarQuery -TargetDir $targetDir -Query "MATCH (x:ExternalService) RETURN count(x) AS cnt"

        Write-Host "  [Stats] Files: $fileCount | Nodes: $nodeCount | Rels: $relCount | EP: $epCount | Tbl: $tblCount | Topic: $topCount | ExtSvc: $extCount | Issues: $issueCount | UncoveredDeps: $($uncoveredDeps.Count)" -ForegroundColor Cyan
        if ($uncoveredDeps.Count -gt 0) {
            Write-Host "  [Uncovered] $(($uncoveredDeps | Select-Object -First 12) -join ', ')" -ForegroundColor Magenta
        }

        $allResults[$name] = @{
            lang = $lang
            repo = $repo
            name = $name
            status = "SUCCESS"
            files = $fileCount
            nodes = $nodeCount
            relationships = $relCount
            endpoints = $epCount
            tables = $tblCount
            topics = $topCount
            external_services = $extCount
            scan_seconds = $scanDuration
            issues = $issueCount
            manifest_deps = $manifestDeps
            uncovered_deps = $uncoveredDeps
            timestamp = (Get-Date).ToString("o")
        }

        $allResults | ConvertTo-Json -Depth 6 | Set-Content $resultsJsonPath -Encoding UTF8
        Update-ReportMarkdown
    } finally {
        if (-not $KeepClone -and (Test-Path $targetDir)) {
            Write-Host "  [Cleanup] Deleting cloned repository $targetDir..." -ForegroundColor DarkGray
            Remove-Item -Recurse -Force $targetDir -ErrorAction SilentlyContinue
        }
    }
}

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Green
Write-Host " Batch Completed! Report at: $reportMdPath" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
