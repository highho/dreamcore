# Zip a self-contained Windows x64 player: exe + UnityPlayer.dll + runtime dirs + _Data.
# A Unity player is NOT just "exe + _Data": UnityPlayer.dll must sit next to the exe, and a
# Mono-scripting build needs MonoBleedingEdge/ beside it. Missing either one gives an archive
# that "builds green" and then does nothing on double-click, so this script fails loudly
# instead of shipping a broken release.
# NOTE: keep this file pure ASCII - Windows PowerShell reads .ps1 as GBK without BOM
param(
    [Parameter(Mandatory=$true)][string]$BuildDir,
    [Parameter(Mandatory=$true)][string]$Name
)
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem

$data = Join-Path $BuildDir "${Name}_Data"
$zip  = Join-Path $BuildDir "$Name-win64.zip"
$items = @()

foreach ($f in @("$Name.exe", "UnityPlayer.dll")) {
    $p = Join-Path $BuildDir $f
    if (-not (Test-Path $p)) { Write-Error "missing required player file: $f"; exit 1 }
    $items += $p
}
if (-not (Test-Path $data)) { Write-Error "missing $data"; exit 1 }
$items += $data

# Mono needs MonoBleedingEdge; the D3D12 loader folder is small and keeps older GPUs working.
foreach ($d in @("MonoBleedingEdge", "D3D12")) {
    $p = Join-Path $BuildDir $d
    if (Test-Path $p) { $items += $p } else { Write-Host "note: no $d in build dir, skipped" }
}

if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path $items -DestinationPath $zip -Force

# Product assertion: read the archive back, not the source folder. This is what catches the
# "exe + _Data only" packaging bug that shipped an unrunnable zip before.
$zipItem = [System.IO.Compression.ZipFile]::OpenRead($zip)
try {
    $names = $zipItem.Entries | ForEach-Object { $_.FullName.Replace('\', '/') }
    $count = $zipItem.Entries.Count
} finally { $zipItem.Dispose() }

foreach ($need in @("$Name.exe", "UnityPlayer.dll", "${Name}_Data/globalgamemanagers")) {
    $hit = $names | Where-Object { $_ -eq $need -or $_.EndsWith("/$need") } | Select-Object -First 1
    if (-not $hit) { Write-Error "zip is missing '$need' - release would not run"; exit 1 }
}
Write-Host ("zip verified: {0} entries, {1:N1} MB, contains exe + UnityPlayer.dll + {2}_Data" -f `
    $count, ((Get-Item $zip).Length / 1MB), $Name)
Write-Host "zip done: $zip"
