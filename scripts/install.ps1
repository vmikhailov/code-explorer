# CodeExplorer ('ce') Windows Installer
$ErrorActionPreference = "Stop"

$repo = "vmikhailov/code-explorer"
$target = "win-x64"

Write-Host "=== CodeExplorer ('ce') Windows Installer ===" -ForegroundColor Cyan

$installDir = if ($env:CE_INSTALL_DIR) {
    $env:CE_INSTALL_DIR
} else {
    Join-Path $env:LOCALAPPDATA "CodeExplorer\bin"
}

Write-Host "Fetching latest release from GitHub ($repo)..."
$releaseUri = "https://api.github.com/repos/$repo/releases/latest"

try {
    $release = Invoke-RestMethod -Uri $releaseUri -Headers @{ "User-Agent" = "CodeExplorer-Installer" }
} catch {
    Write-Host "Failed to query GitHub API: $_" -ForegroundColor Red
    exit 1
}

$asset = $release.assets | Where-Object { $_.name -like "ce-$target.zip" } | Select-Object -First 1

if (-not $asset) {
    Write-Host "Error: Could not find release asset 'ce-$target.zip' in latest release." -ForegroundColor Red
    Write-Host "Check available releases at: https://github.com/$repo/releases"
    exit 1
}

$tempZip = Join-Path ([System.IO.Path]::GetTempPath()) "ce-win-x64-$([System.Guid]::NewGuid().ToString('N')).zip"
$tempExtract = Join-Path ([System.IO.Path]::GetTempPath()) "ce-extract-$([System.Guid]::NewGuid().ToString('N'))"

try {
    Write-Host "Downloading $($asset.browser_download_url)..."
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $tempZip -UseBasicParsing

    Write-Host "Extracting..."
    Expand-Archive -Path $tempZip -DestinationPath $tempExtract -Force

    if (-not (Test-Path $installDir)) {
        New-Item -ItemType Directory -Path $installDir -Force | Out-Null
    }

    $sourceExe = Join-Path $tempExtract "ce.exe"
    $targetExe = Join-Path $installDir "ce.exe"

    Copy-Item -Path $sourceExe -Destination $targetExe -Force
    Write-Host "[OK] Successfully installed CodeExplorer to $targetExe" -ForegroundColor Green

    # Check and add to User PATH if missing
    $userPath = [Environment]::GetEnvironmentVariable("Path", "User")
    if ($userPath -notlike "*$installDir*") {
        Write-Host "Adding $installDir to User PATH..." -ForegroundColor Yellow
        [Environment]::SetEnvironmentVariable("Path", "$userPath;$installDir", "User")
        $env:Path = "$env:Path;$installDir"
        Write-Host "[OK] Added to PATH. (You may need to restart existing terminal windows)" -ForegroundColor Green
    }

    Write-Host ""
    Write-Host "To verify installation, run:"
    Write-Host "  ce --version"
} finally {
    if (Test-Path $tempZip) { Remove-Item -Path $tempZip -Force -ErrorAction SilentlyContinue }
    if (Test-Path $tempExtract) { Remove-Item -Path $tempExtract -Recurse -Force -ErrorAction SilentlyContinue }
}
