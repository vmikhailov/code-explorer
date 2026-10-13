<#
.SYNOPSIS
    GitHub Issue Triage & Management Helper for code-explorer.
    Used by the Agy scheduled automation and for manual triage workflows.

.DESCRIPTION
    Provides helper commands to interact with the GitHub REST API for:
    - Checking daytime/nighttime schedule cadence.
    - Fetching open, untriaged issues.
    - Adding issue comments signed by Agy.
    - Applying triage and complexity labels.
    - Opening Pull Requests for automated bug fixes.
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet("check-cadence", "list-issues", "get-issue", "comment", "label", "create-pr", "create-worktree", "remove-worktree")]
    [string]$Action = "list-issues",

    [Parameter()]
    [string]$Repo = "vmikhailov/code-explorer",

    [Parameter()]
    [int]$IssueNumber,

    [Parameter()]
    [string]$Body,

    [Parameter()]
    [string[]]$Labels,

    [Parameter()]
    [string]$HeadBranch,

    [Parameter()]
    [string]$BaseBranch = "main",

    [Parameter()]
    [string]$Title,

    [Parameter()]
    [int]$MinIssueNumber = 5,

    [Parameter()]
    [int[]]$IgnoreIssueNumbers = @(3, 4)
)

$ErrorActionPreference = "Stop"

function Get-GitHubHeaders([switch]$RequireAuth) {
    $token = $env:GITHUB_TOKEN
    if ([string]::IsNullOrWhiteSpace($token)) {
        try {
            $inputData = @"
protocol=https
host=github.com
username=vmikhailov
"@
            $credLine = ($inputData | git credential fill 2>$null | Select-String "password=")
            if ($credLine) {
                $token = $credLine.ToString().Replace("password=", "").Trim()
            }
        } catch {
            # Git Credential Manager not found or error
        }
    }

    $headers = @{
        "Accept"               = "application/vnd.github+json"
        "X-GitHub-Api-Version" = "2022-11-28"
        "User-Agent"           = "Agy-Issue-Triage"
    }
    if (-not [string]::IsNullOrWhiteSpace($token)) {
        $headers["Authorization"] = "Bearer $token"
    } elseif ($RequireAuth) {
        Write-Error "GitHub token could not be obtained from GITHUB_TOKEN or Git Credential Manager. A GitHub Personal Access Token is required for write operations (commenting, labeling, creating PRs)."
        exit 1
    } else {
        Write-Host "Notice: GitHub token not found, performing unauthenticated read." -ForegroundColor Yellow
    }
    return $headers
}

function Test-ScheduleCadence {
    $now = Get-Date
    $hour = $now.Hour
    $minute = $now.Minute

    # Day: 08:00 - 22:59 -> runs every 15 min (always true)
    if ($hour -ge 8 -and $hour -lt 23) {
        Write-Host "RUN: Daytime ($hour : $minute) - 15-minute interval"
        return $true
    }

    # Night: 23:00 - 07:59 -> runs once an hour (at :00 - :14 window)
    if ($minute -lt 15) {
        Write-Host "RUN: Nighttime ($hour : $minute) - top-of-hour interval"
        return $true
    }

    Write-Host "SKIP: Nighttime ($hour : $minute) - waiting for next hour"
    return $false
}

switch ($Action) {
    "check-cadence" {
        $shouldRun = Test-ScheduleCadence
        if (-not $shouldRun) {
            exit 2 # Exit code 2 indicates skip
        }
    }

    "list-issues" {
        $headers = Get-GitHubHeaders
        $uri = "https://api.github.com/repos/$Repo/issues?state=open&per_page=50"
        $issues = Invoke-RestMethod -Uri $uri -Headers $headers -Method Get

        # Filter out PRs, existing in-progress issues, assigned issues, and triaged/wip issues
        $filtered = $issues | Where-Object {
            $num = [int]$_.number
            $null -eq $_.pull_request -and
            $num -ge $MinIssueNumber -and
            -not ($IgnoreIssueNumbers -contains $num) -and
            -not ($_.assignees -and $_.assignees.Count -gt 0) -and
            -not ($_.labels | Where-Object { $_.name -in @("triaged", "in-progress", "wip", "assigned") })
        }

        # Double check origin branches to avoid stepping on existing PRs/branches
        $unclaimed = @()
        foreach ($issue in $filtered) {
            $branchCheck = git ls-remote --heads origin "fix/issue-$($issue.number)" 2>$null
            if (-not [string]::IsNullOrWhiteSpace($branchCheck)) {
                Write-Host "Skipping issue #$($issue.number): branch fix/issue-$($issue.number) already exists on origin." -ForegroundColor Yellow
                continue
            }
            $unclaimed += $issue
        }

        if (-not $unclaimed -or $unclaimed.Count -eq 0) {
            Write-Output "No untriaged issues found."
            return
        }

        $unclaimed | Select-Object number, title, html_url, @{Name="author"; Expression={$_.user.login}}, created_at | ConvertTo-Json -Depth 3
    }

    "create-worktree" {
        if (-not $IssueNumber) { throw "Missing -IssueNumber parameter." }
        $worktreePath = Join-Path $env:TEMP "ce-fix-$IssueNumber"
        if (Test-Path $worktreePath) {
            git worktree remove $worktreePath --force 2>$null
            if (Test-Path $worktreePath) { Remove-Item -Recurse -Force $worktreePath }
        }
        git fetch origin main 2>$null
        git worktree add $worktreePath -b "fix/issue-$IssueNumber" origin/main
        Write-Output $worktreePath
    }

    "remove-worktree" {
        if (-not $IssueNumber) { throw "Missing -IssueNumber parameter." }
        $worktreePath = Join-Path $env:TEMP "ce-fix-$IssueNumber"
        if (Test-Path $worktreePath) {
            git worktree remove $worktreePath --force 2>$null
        }
        Write-Output "Worktree removed: $worktreePath"
    }

    "get-issue" {
        if (-not $IssueNumber) { throw "Missing -IssueNumber parameter." }
        $headers = Get-GitHubHeaders
        $uri = "https://api.github.com/repos/$Repo/issues/$IssueNumber"
        $issue = Invoke-RestMethod -Uri $uri -Headers $headers -Method Get
        $issue | Select-Object number, title, body, html_url, @{Name="author"; Expression={$_.user.login}}, labels, created_at | ConvertTo-Json -Depth 4
    }

    "comment" {
        if (-not $IssueNumber) { throw "Missing -IssueNumber parameter." }
        if ([string]::IsNullOrWhiteSpace($Body)) { throw "Missing -Body parameter." }

        $headers = Get-GitHubHeaders -RequireAuth
        $uri = "https://api.github.com/repos/$Repo/issues/$IssueNumber/comments"

        # Ensure signature
        $finalBody = $Body
        if (-not ($finalBody -match "— Agy|Agy\s*$")) {
            $finalBody += "`n`n— Agy"
        }

        $payload = @{ body = $finalBody } | ConvertTo-Json
        $response = Invoke-RestMethod -Uri $uri -Headers $headers -Method Post -Body $payload
        Write-Output "Comment added to issue #$($IssueNumber). Comment URL: $($response.html_url)"
    }

    "label" {
        if (-not $IssueNumber) { throw "Missing -IssueNumber parameter." }
        if (-not $Labels -or $Labels.Count -eq 0) { throw "Missing -Labels parameter." }

        $headers = Get-GitHubHeaders -RequireAuth
        $uri = "https://api.github.com/repos/$Repo/issues/$IssueNumber/labels"
        $payload = @{ labels = $Labels } | ConvertTo-Json
        $response = Invoke-RestMethod -Uri $uri -Headers $headers -Method Post -Body $payload
        Write-Output "Labels added to issue #$($IssueNumber): $($Labels -join ', ')"
    }

    "create-pr" {
        if ([string]::IsNullOrWhiteSpace($HeadBranch)) { throw "Missing -HeadBranch parameter." }
        if ([string]::IsNullOrWhiteSpace($Title)) { throw "Missing -Title parameter." }

        $headers = Get-GitHubHeaders -RequireAuth
        $uri = "https://api.github.com/repos/$Repo/pulls"

        $prBody = if ($Body) { $Body } else { "Automated fix by Agy.`n`n— Agy" }
        if (-not ($prBody -match "— Agy|Agy\s*$")) {
            $prBody += "`n`n— Agy"
        }

        $payload = @{
            title = $Title
            head  = $HeadBranch
            base  = $BaseBranch
            body  = $prBody
        } | ConvertTo-Json

        $response = Invoke-RestMethod -Uri $uri -Headers $headers -Method Post -Body $payload
        Write-Output "Pull Request #$($response.number) created: $($response.html_url)"
    }
}
