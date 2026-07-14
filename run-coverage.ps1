param(
    [string]$Configuration = "Debug",
    [double]$MinimumLineCoverage = 38,
    [double]$MinimumBranchCoverage = 27,
    [double]$MinimumMethodCoverage = 55
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$results = Join-Path $root "TestResults\Coverage"

if (Test-Path -LiteralPath $results) {
    Remove-Item -LiteralPath $results -Recurse -Force
}

$coverageOutput = Join-Path $results "coverage.cobertura.xml"
dotnet test (Join-Path $root "DansRBIBaseball2026.sln") `
    -c $Configuration `
    /p:CollectCoverage=true `
    /p:CoverletOutputFormat=cobertura `
    "/p:CoverletOutput=$coverageOutput"

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$report = Get-ChildItem -LiteralPath $results -Recurse -Filter "coverage.cobertura.xml" |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1
if (-not $report) {
    throw "Coverage collection completed without producing coverage.cobertura.xml."
}

[xml]$coverageXml = Get-Content -LiteralPath $report.FullName
$coverage = $coverageXml.coverage
$validLines = [int]$coverage.'lines-valid'
if ($validLines -le 0) {
    throw "Coverage report is empty: no valid application lines were instrumented."
}

$lineRate = [double]$coverage.'line-rate' * 100
$branchRate = [double]$coverage.'branch-rate' * 100
$methods = @($coverageXml.SelectNodes("//method"))
$coveredMethods = @($methods | Where-Object {
    @($_.SelectNodes("./lines/line") | Where-Object { [int]$_.hits -gt 0 }).Count -gt 0
})
$methodRate = if ($methods.Count -gt 0) {
    100 * $coveredMethods.Count / $methods.Count
}
else {
    0
}
Write-Host ("Coverage report: {0}" -f $report.FullName)
Write-Host ("Line coverage: {0:N2}% ({1}/{2})" -f $lineRate, $coverage.'lines-covered', $validLines)
Write-Host ("Branch coverage: {0:N2}% ({1}/{2})" -f $branchRate, $coverage.'branches-covered', $coverage.'branches-valid')
Write-Host ("Method coverage: {0:N2}% ({1}/{2})" -f $methodRate, $coveredMethods.Count, $methods.Count)

$failures = @()
if ($lineRate -lt $MinimumLineCoverage) {
    $failures += ("Line coverage {0:N2}% is below the {1:N2}% minimum." -f $lineRate, $MinimumLineCoverage)
}
if ($branchRate -lt $MinimumBranchCoverage) {
    $failures += ("Branch coverage {0:N2}% is below the {1:N2}% minimum." -f $branchRate, $MinimumBranchCoverage)
}
if ($methodRate -lt $MinimumMethodCoverage) {
    $failures += ("Method coverage {0:N2}% is below the {1:N2}% minimum." -f $methodRate, $MinimumMethodCoverage)
}
if ($failures.Count -gt 0) {
    throw "Coverage regression detected:`n$($failures -join "`n")"
}
