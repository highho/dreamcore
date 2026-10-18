# Probe: does the title screen auto-advance with NO input at all?
# Takes four screenshots of the player without sending a single key or mouse event,
# so "the corridor showed up on the title's own" can be timed instead of argued.
# ASCII only (PowerShell 5.1 reads .ps1 as ANSI without a BOM).
param(
  [Parameter(Mandatory = $true)][string]$Exe,
  [Parameter(Mandatory = $true)][string]$OutDir,
  [int]$BootWaitSec = 12
)
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Win32Probe {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
}
"@
if (-not (Test-Path $Exe)) { throw "no exe: $Exe" }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$Exe = (Resolve-Path $Exe).Path
$OutDir = (Resolve-Path $OutDir).Path
$bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds

function Take-Shot([string]$name) {
  $path = Join-Path $OutDir $name
  $bmp = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen(0, 0, 0, 0, $bmp.Size)
  $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
  $g.Dispose(); $bmp.Dispose()
  Write-Host ("SHOT {0} bytes={1}" -f $name, (Get-Item $path).Length)
}

$p = Start-Process $Exe -ArgumentList @(
  "-screen-fullscreen", "0", "-screen-width", "1280", "-screen-height", "720",
  "-logFile", (Join-Path $OutDir "player.log")
) -PassThru
try {
  Start-Sleep -Seconds $BootWaitSec
  [void][Win32Probe]::SetForegroundWindow($p.MainWindowHandle)
  Start-Sleep -Milliseconds 500
  Take-Shot "t0.png"
  Start-Sleep -Seconds 6
  Take-Shot "t6.png"
  Start-Sleep -Seconds 6
  Take-Shot "t12.png"
  Start-Sleep -Seconds 12
  Take-Shot "t24.png"
}
finally {
  if (-not $p.HasExited) { [void]$p.Kill(); Start-Sleep -Seconds 2 }
  Write-Host "done"
}
