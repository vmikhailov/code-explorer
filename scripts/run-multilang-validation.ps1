[CmdletBinding()]
param(
    [string]$Language = "",
    [int]$Limit = 0,
    [string]$ReposFile = "C:\Work\Personal\code-explorer-wt-multilang-scan\scripts\multilang-repositories.json",
    [string]$TestingDir = "C:\Work\Personal\code-explorer-testing",
    [string]$CeDll = "C:\Work\Personal\code-explorer-wt-multilang-scan\cli\.Build\bin_Release_AnyCPU\CodeExplorer\ce.dll",
    [int]$CleanThresholdGB = 50,
    [switch]$ForceRescan
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

$reposJson = Get-Content $ReposFile -Raw | ConvertFrom-Json
if (-not [string]::IsNullOrEmpty($Language)) {
    $reposJson = $reposJson | Where-Object { $_.lang -eq $Language }
}
if ($Limit -gt 0) {
    $reposJson = $reposJson | Select-Object -First $Limit
}

$total = $reposJson.Count
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Starting Multi-Language CodeExplorer Validation Suite" -ForegroundColor Cyan
Write-Host " Target Repositories: $total" -ForegroundColor Cyan
Write-Host " Testing Base Dir:    $TestingDir" -ForegroundColor Cyan
Write-Host " CE Binary:           $CeDll" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

function Check-DiskSpace {
    $drive = Get-PSDrive C
    $freeGB = [math]::Round($drive.Free / 1GB, 2)
    Write-Host "[Disk] Free space on C:: ${freeGB} GB" -ForegroundColor Gray
    if ($freeGB -lt $CleanThresholdGB) {
        Write-Warning "[Disk Warning] Free space ($freeGB GB) below threshold ($CleanThresholdGB GB). Cleaning past test repos..."
        $completedDirs = Get-ChildItem -Directory $TestingDir
        foreach ($d in $completedDirs) {
            if ($allResults.ContainsKey($d.Name) -and $allResults[$d.Name].status -eq "SUCCESS") {
                Write-Host "[Cleanup] Removing completed test repo: $($d.FullName)" -ForegroundColor Yellow
                Remove-Item -Recurse -Force $d.FullName -ErrorAction SilentlyContinue
                $newFree = [math]::Round((Get-PSDrive C).Free / 1GB, 2)
                if ($newFree -gt ($CleanThresholdGB + 20)) { break }
            }
        }
    }
}

function Update-ReportMarkdown {
    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.AppendLine("# CodeExplorer Multi-Language Robustness & Completeness Report")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("Automated overnight validation across top open-source repositories.")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("| # | Language | Repository | Files | Nodes | Relationships | Scan Time | Issues | Status |")
    [void]$sb.AppendLine("|---|---|---|---|---|---|---|---|---|")

    $idx = 1
    $langs = $reposJson | Select-Object -ExpandProperty lang -Unique
    foreach ($entry in ($allResults.Values | Sort-Object timestamp)) {
        $filesStr = if ($entry.files) { $entry.files } else { "-" }
        $nodesStr = if ($entry.nodes) { "{0:N0}" -f $entry.nodes } else { "-" }
        $relsStr = if ($entry.relationships) { "{0:N0}" -f $entry.relationships } else { "-" }
        $timeStr = if ($entry.scan_seconds) { "$($entry.scan_seconds) s" } else { "-" }
        $issuesStr = if ($entry.issues -gt 0) { "**$($entry.issues)**" } else { "0" }
        $statusIcon = if ($entry.status -eq "SUCCESS") { "PASSED" } else { "**FAILED**" }

        [void]$sb.AppendLine("| $idx | $($entry.lang) | [$($entry.repo)](https://github.com/$($entry.repo)) | $filesStr | $nodesStr | $relsStr | $timeStr | $issuesStr | $statusIcon |")
        $idx++
    }

    $sb.ToString() | Set-Content $reportMdPath -Encoding UTF8
}

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
            Write-Host "  -> Already verified successfully ($($existing.nodes) nodes, $($existing.relationships) rels). Skipping." -ForegroundColor DarkGray
            continue
        }
    }

    Check-DiskSpace

    # 1. Clone
    if (-not (Test-Path $targetDir)) {
        Write-Host "  [Git] Cloning https://github.com/$repo.git (--depth 1)..." -ForegroundColor Gray
        $cloneStart = Get-Date
        & git clone --depth 1 "https://github.com/$repo.git" $targetDir
        if ($LASTEXITCODE -ne 0) {
            Write-Error "  [Git Error] Failed to clone $repo"
            $allResults[$name] = @{
                lang = $lang
                repo = $repo
                name = $name
                status = "FAILED"
                error = "Git clone failed"
                timestamp = (Get-Date).ToString("o")
            }
            $allResults | ConvertTo-Json -Depth 5 | Set-Content $resultsJsonPath -Encoding UTF8
            Update-ReportMarkdown
            continue
        }
        $cloneDuration = [math]::Round(((Get-Date) - $cloneStart).TotalSeconds, 1)
        Write-Host "  [Git] Cloned in ${cloneDuration}s" -ForegroundColor Gray
    } else {
        Write-Host "  [Git] Directory already exists: $targetDir" -ForegroundColor Gray
    }

    # 2. Init
    & dotnet $CeDll init --dir $targetDir | Out-Null

    # 3. Scan
    Write-Host "  [CE] Scanning workspace..." -ForegroundColor Yellow
    $scanStart = Get-Date
    $scanOutput = & dotnet $CeDll scan --clear $targetDir 2>&1
    $scanExitCode = $LASTEXITCODE
    $scanDuration = [math]::Round(((Get-Date) - $scanStart).TotalSeconds, 1)

    if ($scanExitCode -ne 0) {
        Write-Host "  [CE FAILED] Scan exited with code $scanExitCode in ${scanDuration}s" -ForegroundColor Red
        $allResults[$name] = @{
            lang = $lang
            repo = $repo
            name = $name
            status = "FAILED"
            scan_seconds = $scanDuration
            error = ($scanOutput | Out-String).Trim()
            timestamp = (Get-Date).ToString("o")
        }
        $allResults | ConvertTo-Json -Depth 5 | Set-Content $resultsJsonPath -Encoding UTF8
        Update-ReportMarkdown
        continue
    }

    Write-Host "  [CE] Scan completed in ${scanDuration}s" -ForegroundColor Green

    # 4. Extract Stats via Cypher Queries
    $nodeCount = 0
    $relCount = 0
    $fileCount = 0
    $issueCount = 0
    $sampleIssues = @()

    try {
        $qNodes = & dotnet $CeDll query --dir $targetDir "MATCH (n) RETURN count(n)" 2>&1 | Out-String
        if ($qNodes -match '(?:cnt:\s*|-\s*)(\d+)') { $nodeCount = [int]$matches[1] }

        $qRels = & dotnet $CeDll query --dir $targetDir "MATCH ()-[r]->() RETURN count(r)" 2>&1 | Out-String
        if ($qRels -match '(?:cnt:\s*|-\s*)(\d+)') { $relCount = [int]$matches[1] }

        $qFiles = & dotnet $CeDll query --dir $targetDir "MATCH (f:File) RETURN count(f)" 2>&1 | Out-String
        if ($qFiles -match '(?:cnt:\s*|-\s*)(\d+)') { $fileCount = [int]$matches[1] }

        $qIssues = & dotnet $CeDll query --dir $targetDir "MATCH (i:ScanIssue) RETURN count(i)" 2>&1 | Out-String
        if ($qIssues -match '(?:cnt:\s*|-\s*)(\d+)') { $issueCount = [int]$matches[1] }
    } catch {
        Write-Warning "  Could not query graph stats: $_"
    }

    Write-Host "  [Stats] Files: $fileCount | Nodes: $nodeCount | Relationships: $relCount | Issues: $issueCount" -ForegroundColor Cyan

    $allResults[$name] = @{
        lang = $lang
        repo = $repo
        name = $name
        status = "SUCCESS"
        files = $fileCount
        nodes = $nodeCount
        relationships = $relCount
        scan_seconds = $scanDuration
        issues = $issueCount
        timestamp = (Get-Date).ToString("o")
    }

    $allResults | ConvertTo-Json -Depth 5 | Set-Content $resultsJsonPath -Encoding UTF8
    Update-ReportMarkdown
}

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Green
Write-Host " Suite Execution Completed! Report at: $reportMdPath" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
