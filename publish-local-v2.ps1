param(
    [string]$OutputPath = (Join-Path $PSScriptRoot "artifacts\local-v2.0"),
    [switch]$BuildInstaller,
    [string]$InstallerOutputPath = (Join-Path $PSScriptRoot "artifacts\installers\local-v2.0"),
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
$installerDefinition = Join-Path $PSScriptRoot "packaging\installer\LocalV2.iss"

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
        throw "Local Version 2.0 locked restore failed with exit code $LASTEXITCODE."
    }

    & $dotnet publish $project `
        --configuration Release `
        --runtime win-x64 `
        --no-restore `
        -p:PublishProfile=LocalV2SingleFile `
        -p:RestoreLockedMode=true `
        "-p:PublishDir=$publishDirectoryForMsBuild"
    if ($LASTEXITCODE -ne 0) {
        throw "Local Version 2.0 publish failed with exit code $LASTEXITCODE."
    }

    $sourceAssets = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot "Assets") -Recurse -File)
    $missingAssets = @($sourceAssets | Where-Object {
        $relativePath = [IO.Path]::GetRelativePath($projectRoot, $_.FullName)
        -not (Test-Path -LiteralPath (Join-Path $publishPath $relativePath))
    })
    if ($missingAssets.Count -gt 0) {
        $paths = $missingAssets | ForEach-Object { [IO.Path]::GetRelativePath($projectRoot, $_.FullName) }
        throw "Local Version 2.0 publish is missing $($missingAssets.Count) asset(s):`n$($paths -join "`n")"
    }

    $changedAssets = @($sourceAssets | Where-Object {
        $relativePath = [IO.Path]::GetRelativePath($projectRoot, $_.FullName)
        $publishedPath = Join-Path $publishPath $relativePath
        (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash -ne
            (Get-FileHash -LiteralPath $publishedPath -Algorithm SHA256).Hash
    })
    if ($changedAssets.Count -gt 0) {
        $paths = $changedAssets | ForEach-Object { [IO.Path]::GetRelativePath($projectRoot, $_.FullName) }
        throw "Local Version 2.0 publish changed $($changedAssets.Count) asset(s):`n$($paths -join "`n")"
    }

    $executable = Join-Path $publishPath "DanVille50RBIbaseball.exe"
    $version = (Get-Item -LiteralPath $executable).VersionInfo
    if ($version.FileVersion -ne "2.0.0.0") {
        throw "Local executable version is $($version.FileVersion); expected 2.0.0.0."
    }

    if ($Sign) {
        & $signingScript -FilePath $executable -ConfigPath $SigningConfigPath
    }
    Write-ChecksumManifest $publishPath

    if ($BuildInstaller) {
        Reset-ArtifactDirectory $installerOutput
        & $iscc "/Qp" "/DSourceDir=$publishPath" "/DOutputDir=$installerOutput" $installerDefinition
        if ($LASTEXITCODE -ne 0) {
            throw "Local Version 2.0 installer build failed with exit code $LASTEXITCODE."
        }
        $installer = Join-Path $installerOutput "Dans-RBI-Baseball-2026-Local-2.0-Setup.exe"
        if (-not (Test-Path -LiteralPath $installer)) {
            throw "Local installer compiler completed without producing $installer."
        }
        if ($Sign) {
            & $signingScript -FilePath $installer -ConfigPath $SigningConfigPath
        }
        Write-ChecksumManifest $installerOutput
        Write-Host "Local-only Version 2.0 installer complete: $installer"
    }

    Write-Host "Local-only Version 2.0 publish complete: $publishPath"
    Write-Host "Verified locked restore, version metadata, checksums, and $($sourceAssets.Count) byte-identical local assets."
}
finally {
    Pop-Location
}
