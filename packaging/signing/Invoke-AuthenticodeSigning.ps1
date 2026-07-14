param(
    [string]$FilePath,
    [Parameter(Mandatory = $true)]
    [string]$ConfigPath,
    [switch]$ValidateOnly
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ConfigPath -PathType Leaf)) {
    throw "Signing was explicitly requested, but the signing configuration does not exist: $ConfigPath"
}

$configPathFull = [IO.Path]::GetFullPath($ConfigPath)
$configDirectory = Split-Path -Parent $configPathFull
$config = Import-PowerShellDataFile -LiteralPath $configPathFull

function Get-ConfiguredOrEnvironmentValue([string]$ConfiguredValue, [string]$EnvironmentVariableName) {
    if (-not [string]::IsNullOrWhiteSpace($ConfiguredValue)) {
        return $ConfiguredValue.Trim()
    }
    if (-not [string]::IsNullOrWhiteSpace($EnvironmentVariableName)) {
        $environmentValue = [Environment]::GetEnvironmentVariable($EnvironmentVariableName)
        if (-not [string]::IsNullOrWhiteSpace($environmentValue)) {
            return $environmentValue.Trim()
        }
    }
    return ""
}

function Resolve-ConfiguredPath([string]$PathValue) {
    if ([IO.Path]::IsPathRooted($PathValue)) {
        return [IO.Path]::GetFullPath($PathValue)
    }
    return [IO.Path]::GetFullPath((Join-Path $configDirectory $PathValue))
}

function Find-SignTool([string]$ConfiguredPath) {
    if (-not [string]::IsNullOrWhiteSpace($ConfiguredPath)) {
        $resolved = Resolve-ConfiguredPath $ConfiguredPath
        if (-not (Test-Path -LiteralPath $resolved -PathType Leaf)) {
            throw "Configured SignToolPath does not exist: $resolved"
        }
        return $resolved
    }

    $command = Get-Command signtool -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $kitsBin = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"
    if (Test-Path -LiteralPath $kitsBin) {
        $tool = Get-ChildItem -LiteralPath $kitsBin -Directory |
            Sort-Object Name -Descending |
            ForEach-Object { Join-Path $_.FullName "x64\signtool.exe" } |
            Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
            Select-Object -First 1
        if ($tool) {
            return $tool
        }
    }

    throw "Signing was explicitly requested, but signtool.exe was not found. Install the Windows SDK, set SignToolPath, or add signtool.exe to PATH."
}

$thumbprint = Get-ConfiguredOrEnvironmentValue `
    ([string]$config.CertificateThumbprint) `
    ([string]$config.CertificateThumbprintEnvironmentVariable)
$thumbprint = $thumbprint.Replace(" ", "")
$certificatePath = Get-ConfiguredOrEnvironmentValue `
    ([string]$config.CertificatePath) `
    ([string]$config.CertificatePathEnvironmentVariable)

if ($thumbprint -and $certificatePath) {
    throw "Signing configuration is ambiguous. Set either a certificate thumbprint or a PFX path, not both."
}
if (-not $thumbprint -and -not $certificatePath) {
    throw "Signing was explicitly requested, but no certificate credentials were supplied. Set DANS_RBI_SIGNING_CERTIFICATE_THUMBPRINT or DANS_RBI_SIGNING_CERTIFICATE_PATH."
}

$digestAlgorithm = if ($config.DigestAlgorithm) { ([string]$config.DigestAlgorithm).ToUpperInvariant() } else { "SHA256" }
if ($digestAlgorithm -ne "SHA256") {
    throw "DigestAlgorithm must be SHA256. Configured value: $digestAlgorithm"
}
$timestampUrl = [string]$config.TimestampUrl
if ([string]::IsNullOrWhiteSpace($timestampUrl)) {
    throw "TimestampUrl is required when signing is requested."
}

$signArguments = @("sign", "/fd", $digestAlgorithm, "/td", $digestAlgorithm, "/tr", $timestampUrl)
if ($thumbprint) {
    $storeName = if ($config.CertificateStoreName) { [string]$config.CertificateStoreName } else { "My" }
    $storeLocation = if ($config.CertificateStoreLocation) { [string]$config.CertificateStoreLocation } else { "CurrentUser" }
    if ($storeLocation -notin @("CurrentUser", "LocalMachine")) {
        throw "CertificateStoreLocation must be CurrentUser or LocalMachine. Configured value: $storeLocation"
    }
    $certificateProviderPath = "Cert:\$storeLocation\$storeName\$thumbprint"
    if (-not (Test-Path -LiteralPath $certificateProviderPath)) {
        throw "Signing certificate $thumbprint was not found in $storeLocation\$storeName."
    }
    $signArguments += @("/sha1", $thumbprint, "/s", $storeName)
    if ($storeLocation -eq "LocalMachine") {
        $signArguments += "/sm"
    }
}
else {
    $certificatePath = Resolve-ConfiguredPath $certificatePath
    if (-not (Test-Path -LiteralPath $certificatePath -PathType Leaf)) {
        throw "Configured signing certificate file does not exist: $certificatePath"
    }
    $passwordVariable = [string]$config.CertificatePasswordEnvironmentVariable
    $certificatePassword = if ($passwordVariable) { [Environment]::GetEnvironmentVariable($passwordVariable) } else { "" }
    if ([string]::IsNullOrEmpty($certificatePassword)) {
        throw "Signing was explicitly requested for a PFX, but its password is absent. Set the $passwordVariable environment variable."
    }
    $signArguments += @("/f", $certificatePath, "/p", $certificatePassword)
}

$signTool = Find-SignTool ([string]$config.SignToolPath)
if ($ValidateOnly) {
    Write-Host "Authenticode signing configuration validated."
    return
}

if ([string]::IsNullOrWhiteSpace($FilePath) -or -not (Test-Path -LiteralPath $FilePath -PathType Leaf)) {
    throw "The file requested for signing does not exist: $FilePath"
}
$target = [IO.Path]::GetFullPath($FilePath)
$signArguments += $target

& $signTool @signArguments
if ($LASTEXITCODE -ne 0) {
    throw "Authenticode signing failed for $target with exit code $LASTEXITCODE."
}

& $signTool verify /pa /v $target
if ($LASTEXITCODE -ne 0) {
    throw "Authenticode verification failed for $target with exit code $LASTEXITCODE."
}

Write-Host "Authenticode signature verified: $target"
