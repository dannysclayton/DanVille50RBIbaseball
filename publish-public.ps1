param(
    [string]$OutputPath = (Join-Path $PSScriptRoot "artifacts\public-v1.0"),
    [switch]$UpdateCheckedInRelease,
    [switch]$BuildInstaller,
    [string]$InstallerOutputPath = (Join-Path $PSScriptRoot "artifacts\installers\public-v1.0"),
    [switch]$Sign,
    [string]$SigningConfigPath = (Join-Path $PSScriptRoot "packaging\signing\signing.psd1")
)

$ErrorActionPreference = "Stop"
$expectedSdkVersion = "8.0.422"
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "artifacts"))
$projectRoot = Join-Path $PSScriptRoot "StandaloneBaseball"
$project = Join-Path $projectRoot "StandaloneBaseball.csproj"
$nugetConfig = Join-Path $PSScriptRoot "NuGet.config"
$signingScript = Join-Path $PSScriptRoot "packaging\signing\Invoke-AuthenticodeSigning.ps1"
$installerDefinition = Join-Path $PSScriptRoot "packaging\installer\PublicV1.iss"
$checkedInReleasePath = Join-Path $PSScriptRoot "release\public-v1.0"
$checkedInReleaseStagingPath = Join-Path $PSScriptRoot "release\public-v1.0.staging"
$checkedInReleaseBackupPath = Join-Path $PSScriptRoot "release\public-v1.0.backup"

function Get-PinnedDotNet {
    $candidates = @()
    if ($env:DOTNET_ROOT) {
        $candidates += (Join-Path $env:DOTNET_ROOT "dotnet.exe")
    }
    $candidates += (Join-Path $HOME ".dotnet\dotnet.exe")
    $dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($dotnetCommand) {
        $candidates += $dotnetCommand.Source
    }

    $foundVersions = @()
    foreach ($candidate in ($candidates | Select-Object -Unique)) {
        if (-not (Test-Path -LiteralPath $candidate)) {
            continue
        }
        try {
            $version = (& $candidate --version 2>$null | Select-Object -First 1).Trim()
            $foundVersions += "$candidate ($version)"
            if ($version -eq $expectedSdkVersion) {
                return $candidate
            }
        }
        catch {
            $foundVersions += "$candidate (unusable)"
        }
    }

    $details = if ($foundVersions.Count -gt 0) { $foundVersions -join "; " } else { "no dotnet host found" }
    throw ".NET SDK $expectedSdkVersion is required by global.json. Found: $details. Install it from https://dotnet.microsoft.com/download/dotnet/8.0."
}

function Get-SafeArtifactPath([string]$Path, [string]$Name) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    $artifactPrefix = $artifactRoot + [IO.Path]::DirectorySeparatorChar
    if (-not $fullPath.StartsWith($artifactPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Name must be below the repository artifact directory: $artifactRoot"
    }
    return $fullPath
}

function Reset-ArtifactDirectory([string]$Path) {
    if (Test-Path -LiteralPath $Path) {
        foreach ($executable in @(Get-ChildItem -LiteralPath $Path -File -Filter "*.exe" -ErrorAction SilentlyContinue)) {
            try {
                $stream = [IO.File]::Open($executable.FullName, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
                $stream.Dispose()
            }
            catch {
                throw "Cannot replace the existing public artifact because '$($executable.FullName)' is in use. Close the running game and publish again. The existing artifact was not modified."
            }
        }
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $Path | Out-Null
}

function Get-InnoCompiler {
    $candidates = @()
    if ($env:INNO_SETUP_COMPILER) {
        $candidates += $env:INNO_SETUP_COMPILER
    }
    $command = Get-Command iscc -ErrorAction SilentlyContinue
    if ($command) {
        $candidates += $command.Source
    }
    $candidates += "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    $candidates += "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
    $candidates += "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    $compiler = $candidates | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
    if (-not $compiler) {
        throw "Installer creation was requested, but Inno Setup 6 (ISCC.exe) was not found. Install Inno Setup or set INNO_SETUP_COMPILER."
    }
    return $compiler
}

function Write-ChecksumManifest([string]$Root) {
    $manifestPath = Join-Path $Root "SHA256SUMS.txt"
    $files = Get-ChildItem -LiteralPath $Root -Recurse -File |
        Where-Object { $_.FullName -ne $manifestPath } |
        Sort-Object { [IO.Path]::GetRelativePath($Root, $_.FullName) }
    $lines = @($files | ForEach-Object {
        $relativePath = [IO.Path]::GetRelativePath($Root, $_.FullName).Replace("\", "/")
        "$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant())  $relativePath"
    })
    Set-Content -LiteralPath $manifestPath -Value $lines -Encoding ascii
}

function Assert-ChecksumManifest([string]$Root) {
    $manifestPath = Join-Path $Root "SHA256SUMS.txt"
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw "Checksum manifest is missing: $manifestPath"
    }

    $rootPath = [IO.Path]::GetFullPath($Root)
    $rootPrefix = $rootPath + [IO.Path]::DirectorySeparatorChar
    $manifestEntries = @{}
    foreach ($line in @(Get-Content -LiteralPath $manifestPath)) {
        if ($line -notmatch '^(?<Hash>[0-9a-fA-F]{64})  (?<Path>.+)$') {
            throw "Checksum manifest contains an invalid line: $line"
        }

        $relativePath = $Matches.Path.Replace('/', [IO.Path]::DirectorySeparatorChar)
        if ([IO.Path]::IsPathRooted($relativePath)) {
            throw "Checksum manifest contains a rooted path: $($Matches.Path)"
        }
        $filePath = [IO.Path]::GetFullPath((Join-Path $rootPath $relativePath))
        if (-not $filePath.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Checksum manifest path escapes the release directory: $($Matches.Path)"
        }
        if ($manifestEntries.ContainsKey($Matches.Path)) {
            throw "Checksum manifest contains a duplicate path: $($Matches.Path)"
        }
        $manifestEntries[$Matches.Path] = $Matches.Hash.ToLowerInvariant()
    }

    $files = @(Get-ChildItem -LiteralPath $Root -Recurse -File |
        Where-Object { $_.FullName -ne $manifestPath })
    if ($files.Count -ne $manifestEntries.Count) {
        throw "Checksum manifest contains $($manifestEntries.Count) entries for $($files.Count) release files."
    }
    foreach ($file in $files) {
        $relativePath = [IO.Path]::GetRelativePath($rootPath, $file.FullName).Replace("\", "/")
        if (-not $manifestEntries.ContainsKey($relativePath)) {
            throw "Checksum manifest does not contain release file: $relativePath"
        }
        $actualHash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actualHash -ne $manifestEntries[$relativePath]) {
            throw "Checksum mismatch for release file: $relativePath"
        }
    }
}

function Assert-PublicReleaseArtifact([string]$Root) {
    $restrictedExtensions = @(
        ".mp3", ".wav", ".wma",
        ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".dib",
        ".mp4", ".avi", ".wmv", ".mov"
    )
    $approvedPublicMedia = @(
        "Assets/Trophies/baseball-mvp-trophy-template.jpg",
        "Assets/Loading Screens/season_opening_baseball_is_back.jpg",
        "Assets/Loading Screens/season_opening_danville50.png",
        "Assets/Loading Screens/doubleheader_game_two.jpg"
    )
    $publishedMedia = @(Get-ChildItem -LiteralPath (Join-Path $Root "Assets") -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object {
            $relativePath = [IO.Path]::GetRelativePath($Root, $_.FullName).Replace("\", "/")
            ($restrictedExtensions -contains $_.Extension.ToLowerInvariant()) -and
                ($approvedPublicMedia -notcontains $relativePath)
        })
    if ($publishedMedia.Count -gt 0) {
        $paths = $publishedMedia | ForEach-Object { [IO.Path]::GetRelativePath($Root, $_.FullName) }
        throw "Public publish contains $($publishedMedia.Count) uncleared media file(s):`n$($paths -join "`n")"
    }

    $requiredFiles = @(
        "DanVille50RBIbaseball.exe",
        "THIRD_PARTY_ATTRIBUTIONS.md",
        "MEDIA_LICENSE_RECOMMENDATIONS.md",
        "Assets\Data\schools.csv",
        "Assets\Replay Templates\ReplayTemplate.rbi-replay.json",
        "Assets\Templates\Lineup Card Template.docx",
        "Assets\Trophies\baseball-mvp-trophy-template.jpg",
        "Assets\Loading Screens\season_opening_baseball_is_back.jpg",
        "Assets\Loading Screens\season_opening_danville50.png",
        "Assets\Loading Screens\doubleheader_game_two.jpg",
        "Assets\Gameplay3D\index.html",
        "Assets\Gameplay3D\baseball-character-rig.js",
        "Assets\Gameplay3D\gameplay3d.js",
        "Assets\Gameplay3D\addons\loaders\GLTFLoader.js",
        "Assets\Gameplay3D\addons\utils\SkeletonUtils.js",
        "Assets\Gameplay3D\models\player_base.glb",
        "Assets\Gameplay3D\models\player_run.glb",
        "Assets\Gameplay3D\models\player_walk.glb"
    )
    $missing = @($requiredFiles | Where-Object { -not (Test-Path -LiteralPath (Join-Path $Root $_) -PathType Leaf) })
    if ($missing.Count -gt 0) {
        throw "Public publish is missing required file(s):`n$($missing -join "`n")"
    }

    $executable = Join-Path $Root "DanVille50RBIbaseball.exe"
    $version = (Get-Item -LiteralPath $executable).VersionInfo
    if ($version.FileVersion -ne "1.0.0.0") {
        throw "Public executable version is $($version.FileVersion); expected 1.0.0.0."
    }
}

function Assert-FixedReleaseRefreshPath([string]$Path) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    $allowedPaths = @(
        [IO.Path]::GetFullPath($checkedInReleasePath),
        [IO.Path]::GetFullPath($checkedInReleaseStagingPath),
        [IO.Path]::GetFullPath($checkedInReleaseBackupPath)
    )
    if (-not ($allowedPaths | Where-Object { $_.Equals($fullPath, [StringComparison]::OrdinalIgnoreCase) })) {
        throw "Refusing to modify non-fixed release refresh path: $fullPath"
    }
}

function Remove-FixedReleaseRefreshDirectory([string]$Path) {
    Assert-FixedReleaseRefreshPath $Path
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
}

function Update-CheckedInReleaseAtomically([string]$SourcePath) {
    Assert-FixedReleaseRefreshPath $checkedInReleasePath
    Assert-FixedReleaseRefreshPath $checkedInReleaseStagingPath
    Assert-FixedReleaseRefreshPath $checkedInReleaseBackupPath

    $releaseParent = Split-Path -Parent ([IO.Path]::GetFullPath($checkedInReleasePath))
    foreach ($siblingPath in @($checkedInReleaseStagingPath, $checkedInReleaseBackupPath)) {
        if (-not (Split-Path -Parent ([IO.Path]::GetFullPath($siblingPath))).Equals(
            $releaseParent, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Release staging and backup directories must be siblings of $checkedInReleasePath."
        }
    }

    Remove-FixedReleaseRefreshDirectory $checkedInReleaseStagingPath
    if (Test-Path -LiteralPath $checkedInReleaseBackupPath) {
        if (Test-Path -LiteralPath $checkedInReleasePath) {
            Remove-FixedReleaseRefreshDirectory $checkedInReleaseBackupPath
        }
        else {
            Move-Item -LiteralPath $checkedInReleaseBackupPath -Destination $checkedInReleasePath
        }
    }

    $hadExistingRelease = Test-Path -LiteralPath $checkedInReleasePath
    $backupCreated = $false
    $swapCompleted = $false
    try {
        New-Item -ItemType Directory -Path $checkedInReleaseStagingPath | Out-Null
        Get-ChildItem -LiteralPath $SourcePath -Force |
            Copy-Item -Destination $checkedInReleaseStagingPath -Recurse -Force

        Assert-PublicReleaseArtifact $checkedInReleaseStagingPath
        Assert-ChecksumManifest $checkedInReleaseStagingPath
        Write-ChecksumManifest $checkedInReleaseStagingPath
        Assert-ChecksumManifest $checkedInReleaseStagingPath

        if ($hadExistingRelease) {
            Move-Item -LiteralPath $checkedInReleasePath -Destination $checkedInReleaseBackupPath
            $backupCreated = $true
        }
        Move-Item -LiteralPath $checkedInReleaseStagingPath -Destination $checkedInReleasePath
        $swapCompleted = $true

        if ($backupCreated) {
            Remove-FixedReleaseRefreshDirectory $checkedInReleaseBackupPath
            $backupCreated = $false
        }
    }
    catch {
        $refreshFailure = $_
        $swapCompleted = $false
        if ($backupCreated -and (Test-Path -LiteralPath $checkedInReleaseBackupPath)) {
            try {
                Remove-FixedReleaseRefreshDirectory $checkedInReleasePath
                Move-Item -LiteralPath $checkedInReleaseBackupPath -Destination $checkedInReleasePath
                $backupCreated = $false
            }
            catch {
                throw "Checked-in release refresh failed and rollback also failed. The previous release remains at '$checkedInReleaseBackupPath'. Refresh error: $($refreshFailure.Exception.Message) Rollback error: $($_.Exception.Message)"
            }
        }
        elseif (-not $hadExistingRelease -and -not $swapCompleted) {
            Remove-FixedReleaseRefreshDirectory $checkedInReleasePath
        }
        throw $refreshFailure
    }
    finally {
        Remove-FixedReleaseRefreshDirectory $checkedInReleaseStagingPath
        if ($swapCompleted -and (Test-Path -LiteralPath $checkedInReleaseBackupPath)) {
            Remove-FixedReleaseRefreshDirectory $checkedInReleaseBackupPath
        }
    }

    Write-Host "Checked-in public release refreshed: $checkedInReleasePath"
}

$publishPath = Get-SafeArtifactPath $OutputPath "OutputPath"
$publishDirectoryForMsBuild = [IO.Path]::GetRelativePath($projectRoot, $publishPath) + [IO.Path]::DirectorySeparatorChar
$installerOutput = Get-SafeArtifactPath $InstallerOutputPath "InstallerOutputPath"
if (-not [IO.Path]::IsPathRooted($SigningConfigPath)) {
    $SigningConfigPath = Join-Path $PSScriptRoot $SigningConfigPath
}
$SigningConfigPath = [IO.Path]::GetFullPath($SigningConfigPath)

Push-Location $PSScriptRoot
try {
    $dotnet = Get-PinnedDotNet
    $iscc = if ($BuildInstaller) { Get-InnoCompiler } else { $null }
    if ($Sign) {
        & $signingScript -ConfigPath $SigningConfigPath -ValidateOnly
    }

    Reset-ArtifactDirectory $publishPath
    & $dotnet restore $project --configfile $nugetConfig --runtime win-x64 --locked-mode
    if ($LASTEXITCODE -ne 0) {
        throw "Public locked restore failed with exit code $LASTEXITCODE."
    }

    & $dotnet publish $project `
        --configuration Release `
        --runtime win-x64 `
        --no-restore `
        -p:PublishProfile=PublicV1SingleFile `
        -p:RestoreLockedMode=true `
        "-p:PublishDir=$publishDirectoryForMsBuild"
    if ($LASTEXITCODE -ne 0) {
        throw "Public publish failed with exit code $LASTEXITCODE."
    }

    Get-ChildItem -LiteralPath $publishPath -File -Filter "Microsoft.Web.WebView2*.xml" -ErrorAction SilentlyContinue |
        Remove-Item -Force

    Assert-PublicReleaseArtifact $publishPath

    $executable = Join-Path $publishPath "DanVille50RBIbaseball.exe"

    if ($Sign) {
        & $signingScript -FilePath $executable -ConfigPath $SigningConfigPath
    }
    Write-ChecksumManifest $publishPath

    if ($BuildInstaller) {
        Reset-ArtifactDirectory $installerOutput
        & $iscc "/Qp" "/DSourceDir=$publishPath" "/DOutputDir=$installerOutput" $installerDefinition
        if ($LASTEXITCODE -ne 0) {
            throw "Public installer build failed with exit code $LASTEXITCODE."
        }
        $installer = Join-Path $installerOutput "Dans-RBI-Baseball-2026-Public-1.0-Setup.exe"
        if (-not (Test-Path -LiteralPath $installer)) {
            throw "Public installer compiler completed without producing $installer."
        }
        if ($Sign) {
            & $signingScript -FilePath $installer -ConfigPath $SigningConfigPath
        }
        Write-ChecksumManifest $installerOutput
        Write-Host "Public Version 1.0 installer complete: $installer"
    }

    if ($UpdateCheckedInRelease) {
        Update-CheckedInReleaseAtomically $publishPath
    }

    Write-Host "Public Version 1.0 publish complete: $publishPath"
    Write-Host "Verified locked restore, version metadata, required files, checksums, and absence of uncleared packaged media."
}
finally {
    Pop-Location
}
