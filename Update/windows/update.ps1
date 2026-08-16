<#
.SYNOPSIS
    AxarDB Automatic Update Script for Windows.
.DESCRIPTION
    Checks GitHub Releases for new AxarDB versions, downloads the latest release,
    safely updates application binaries, and preserves all database data and collections.
.PARAMETER InstallDir
    Target installation directory of AxarDB. Defaults to current directory or detected installation.
.PARAMETER ServiceName
    Windows Service name if AxarDB runs as a service (default: AxarDB).
.PARAMETER CheckOnly
    Only check if a new version is available without applying updates.
.PARAMETER Force
    Force update even if the current version matches the latest release.
.PARAMETER RepoOwner
    GitHub repository owner (default: metin-yakar).
.PARAMETER RepoName
    GitHub repository name (default: AxarDB).
#>

[CmdletBinding()]
param (
    [Parameter(Mandatory = $false)]
    [string]$InstallDir = "",

    [Parameter(Mandatory = $false)]
    [string]$ServiceName = "AxarDB",

    [Parameter(Mandatory = $false)]
    [switch]$CheckOnly,

    [Parameter(Mandatory = $false)]
    [switch]$Force,

    [Parameter(Mandatory = $false)]
    [string]$RepoOwner = "metin-yakar",

    [Parameter(Mandatory = $false)]
    [string]$RepoName = "AxarDB"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Log {
    param ([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Write-Host "[$timestamp] [$Level] $Message"
}

# Resolve Installation Directory
if ([string]::IsNullOrWhiteSpace($InstallDir)) {
    if (Test-Path ".\AxarDB.dll") {
        $InstallDir = (Get-Item ".").FullName
    } elseif (Test-Path "..\AxarDB.dll") {
        $InstallDir = (Get-Item "..").FullName
    } elseif (Test-Path "C:\Program Files\AxarDB\AxarDB.dll") {
        $InstallDir = "C:\Program Files\AxarDB"
    } else {
        $InstallDir = (Get-Item ".").FullName
    }
}

$InstallDir = [System.IO.Path]::GetFullPath($InstallDir)
Write-Log "Target installation directory: $InstallDir"

if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}

# Read Current Installed Version
$versionFile = Join-Path $InstallDir "version.txt"
$currentVersion = "none"
if (Test-Path $versionFile) {
    $currentVersion = (Get-Content $versionFile -Raw).Trim()
} elseif (Test-Path (Join-Path $InstallDir "AxarDB.dll")) {
    try {
        $fileVer = (Get-Item (Join-Path $InstallDir "AxarDB.dll")).VersionInfo.FileVersion
        if (![string]::IsNullOrWhiteSpace($fileVer)) {
            $currentVersion = "v" + $fileVer
        }
    } catch {
        $currentVersion = "unknown"
    }
}

Write-Log "Current installed version: $currentVersion"

# Query GitHub Releases API for Latest Release
$apiUrl = "https://api.github.com/repos/$RepoOwner/$RepoName/releases/latest"
Write-Log "Checking for updates from: $apiUrl"

$headers = @{
    "User-Agent" = "AxarDB-Updater-Windows"
    "Accept"     = "application/vnd.github.v3+json"
}

try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 -bor [Net.SecurityProtocolType]::Tls13
    $releaseInfo = Invoke-RestMethod -Uri $apiUrl -Headers $headers -Method Get -TimeoutSec 30
} catch {
    Write-Log "Failed to query GitHub Releases API: $($_.Exception.Message)" "ERROR"
    exit 1
}

$latestVersion = $releaseInfo.tag_name
Write-Log "Latest available release on GitHub: $latestVersion"

# Check if Update is Required
$isNewer = ($currentVersion -ne $latestVersion)
if (-not $isNewer -and -not $Force) {
    Write-Log "AxarDB is already up-to-date ($currentVersion). No action required." "INFO"
    exit 0
}

if ($CheckOnly) {
    Write-Log "Update is available: $currentVersion -> $latestVersion" "INFO"
    exit 0
}

Write-Log "Starting update process: $currentVersion -> $latestVersion" "INFO"

# Locate Windows Release Asset
$asset = $releaseInfo.assets | Where-Object { $_.name -like "*windows*.zip" -or $_.name -like "*win-x64*.zip" } | Select-Object -First 1

if ($null -eq $asset) {
    # Fallback to any zip asset if specific Windows asset name not found
    $asset = $releaseInfo.assets | Where-Object { $_.name -like "*.zip" } | Select-Object -First 1
}

if ($null -eq $asset) {
    Write-Log "No suitable Windows release package found in release $latestVersion" "ERROR"
    exit 1
}

$downloadUrl = $asset.browser_download_url
Write-Log "Downloading release package: $($asset.name) from $downloadUrl"

# Prepare Temporary Staging Workspace
$tempId = [Guid]::NewGuid().ToString("N")
$stagingBase = Join-Path ([System.IO.Path]::GetTempPath()) "axardb_update_$tempId"
$zipPath = Join-Path $stagingBase $asset.name
$extractDir = Join-Path $stagingBase "extracted"

New-Item -ItemType Directory -Path $stagingBase -Force | Out-Null
New-Item -ItemType Directory -Path $extractDir -Force | Out-Null

try {
    # Download Archive
    Invoke-WebRequest -Uri $downloadUrl -OutFile $zipPath -Headers $headers

    Write-Log "Extracting archive to staging directory..."
    Expand-Archive -Path $zipPath -DestinationPath $extractDir -Force

    # Resolve inner root if archive contains a top-level directory (e.g., 'windows/')
    $sourceDir = $extractDir
    $subDirs = Get-ChildItem -Path $extractDir -Directory
    if ($subDirs.Count -eq 1 -and (Test-Path (Join-Path $subDirs[0].FullName "AxarDB.dll"))) {
        $sourceDir = $subDirs[0].FullName
    }

    # Stop AxarDB Service if Running
    $serviceWasRunning = $false
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($null -ne $service) {
        if ($service.Status -eq [System.ServiceProcess.ServiceControllerStatus]::Running) {
            Write-Log "Stopping Windows service '$ServiceName'..."
            Stop-Service -Name $ServiceName -Force
            $service.WaitForStatus([System.ServiceProcess.ServiceControllerStatus]::Stopped, [TimeSpan]::FromSeconds(30))
            $serviceWasRunning = $true
            Write-Log "Service '$ServiceName' stopped successfully."
        }
    } else {
        # Check if standalone process is running
        $runningProcesses = Get-Process -Name "AxarDB" -ErrorAction SilentlyContinue
        if ($runningProcesses) {
            Write-Log "Stopping running AxarDB processes..."
            $runningProcesses | Stop-Process -Force
            Start-Sleep -Seconds 2
        }
    }

    # CRITICAL: Safe Copy Routine (Protect Data and User Directories)
    # The following folders MUST NOT be deleted or overwritten with empty templates
    $protectedFolders = @("Data", "Bulk", "Views", "Triggers", "backup_queries", "request_logs", "error_logs", "debug_logs", "view_logs", "trigger_logs")

    Write-Log "Safely updating binaries and application assets..."

    $itemsToCopy = Get-ChildItem -Path $sourceDir
    foreach ($item in $itemsToCopy) {
        $destPath = Join-Path $InstallDir $item.Name

        if ($item.PSIsContainer) {
            # Skip replacing protected user data directories if they already exist in installation
            if ($protectedFolders -contains $item.Name -and (Test-Path $destPath)) {
                Write-Log "Preserving existing data directory: $($item.Name)" "INFO"
                continue
            }
            # Copy non-protected directories (such as wwwroot, Docs)
            Copy-Item -Path $item.FullName -Destination $destPath -Recurse -Force
        } else {
            # Files: Overwrite binary/assembly files
            # Protect user modified appsettings.json if already present and target does not provide a custom config
            if ($item.Name -eq "appsettings.json" -and (Test-Path $destPath)) {
                Write-Log "Preserving existing appsettings.json configuration file." "INFO"
                continue
            }
            Copy-Item -Path $item.FullName -Destination $destPath -Force
        }
    }

    # Record New Version
    Set-Content -Path $versionFile -Value $latestVersion -Encoding UTF8
    Write-Log "Version file updated to $latestVersion."

    # Restart Service if Applicable
    if ($serviceWasRunning -and $null -ne $service) {
        Write-Log "Starting Windows service '$ServiceName'..."
        Start-Service -Name $ServiceName
        $service.WaitForStatus([System.ServiceProcess.ServiceControllerStatus]::Running, [TimeSpan]::FromSeconds(30))
        Write-Log "Service '$ServiceName' started successfully."
    }

    Write-Log "Update to $latestVersion completed successfully without data loss." "INFO"
}
catch {
    Write-Log "Update failed: $($_.Exception.Message)" "ERROR"
    if ($serviceWasRunning -and $null -ne $service) {
        Write-Log "Attempting to restart service '$ServiceName'..." "WARN"
        Start-Service -Name $ServiceName -ErrorAction SilentlyContinue
    }
    exit 1
}
finally {
    # Cleanup temporary files
    if (Test-Path $stagingBase) {
        Remove-Item -Path $stagingBase -Recurse -Force -ErrorAction SilentlyContinue
    }
}
