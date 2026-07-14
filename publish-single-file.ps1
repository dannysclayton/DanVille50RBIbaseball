param(
    [string]$OutputPath = (Join-Path $PSScriptRoot "artifacts\public-v1.0")
)

& (Join-Path $PSScriptRoot "publish-public.ps1") -OutputPath $OutputPath
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
