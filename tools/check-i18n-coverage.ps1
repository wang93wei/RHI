#Requires -Version 5.1
# PowerShell wrapper for check-i18n-coverage.py
$ErrorActionPreference = "Stop"
$base = Join-Path $PSScriptRoot ".." "RenoDXCommander" "Assets" "Languages"
$enPath = Join-Path $base "en-US.json"
if (-not (Test-Path $enPath)) { Write-Error "Missing $enPath"; exit 1 }
$enData = Get-Content $enPath -Raw | ConvertFrom-Json
$total = ($enData.PSObject.Properties | Measure-Object).Count
Write-Host "[check-i18n] en-US baseline: $total keys"
$langs = @("zh-CN","zh-TW","ja-JP","ko-KR")
foreach ($lang in $langs) {
    $p = Join-Path $base "$lang.json"
    if (-not (Test-Path $p)) { Write-Host "[check-i18n] $lang: MISSING FILE"; continue }
    $data = Get-Content $p -Raw | ConvertFrom-Json
    $present = 0
    $missing = @()
    foreach ($k in $enData.PSObject.Properties.Name) {
        if ($data.PSObject.Properties[$k] -and -not [string]::IsNullOrWhiteSpace($data.$k)) { $present++ } else { $missing += $k }
    }
    $cov = if ($total -gt 0) { $present / $total } else { 0 }
    Write-Host ("[check-i18n] {0}: {1}/{2} ({3:P1})" -f $lang, $present, $total, $cov)
    if ($missing.Count -gt 0) {
        Write-Host ("  Missing ({0}): {1}" -f $missing.Count, ($missing[0..9] -join ", "))
    }
}
Write-Host "[check-i18n] Done"
