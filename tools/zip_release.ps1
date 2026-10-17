# Zip exe + _Data folder into one archive (called by build.sh)
# NOTE: keep this file pure ASCII - Windows PowerShell reads .ps1 as GBK without BOM
param(
    [Parameter(Mandatory=$true)][string]$BuildDir,
    [Parameter(Mandatory=$true)][string]$Name
)
$exe  = Join-Path $BuildDir "$Name.exe"
$data = Join-Path $BuildDir "${Name}_Data"
$zip  = Join-Path $BuildDir "$Name-win64.zip"
if (-not (Test-Path $exe))  { Write-Error "missing $exe";  exit 1 }
if (-not (Test-Path $data)) { Write-Error "missing $data"; exit 1 }
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path $exe, $data -DestinationPath $zip -Force
Write-Host "zip done: $zip"
