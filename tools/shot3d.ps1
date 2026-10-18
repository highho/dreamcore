# Real-machine smoke test for the 3D Windows build.
# Launches the player windowed (so GDI screen capture works), takes screenshots,
# drives it with synthesized keys/mouse, and hashes the frames so "the camera moved"
# is a reading rather than an opinion. ASCII only: PowerShell 5.1 reads .ps1 without a
# BOM as the ANSI codepage, where CJK comments break the parser.
param(
  [Parameter(Mandatory = $true)][string]$Exe,
  [Parameter(Mandatory = $true)][string]$OutDir,
  [int]$BootWaitSec = 20,
  [int]$Width = 1280,
  [int]$Height = 720,
  [int]$QuitWaitMs = 30000
)
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Win32Shot {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern void keybd_event(byte k, byte s, uint f, UIntPtr e);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
}
"@

if (-not (Test-Path $Exe)) { throw "no exe: $Exe" }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$Exe = (Resolve-Path $Exe).Path
$OutDir = (Resolve-Path $OutDir).Path
$bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
Write-Host "desktop $($bounds.Width)x$($bounds.Height)"

function Take-Shot([string]$name) {
  $path = Join-Path $OutDir $name
  $bmp = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen(0, 0, 0, 0, $bmp.Size)
  $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
  $g.Dispose(); $bmp.Dispose()
  $h = (Get-FileHash $path -Algorithm MD5).Hash
  Write-Host ("SHOT {0} md5={1} bytes={2}" -f $name, $h, (Get-Item $path).Length)
  return $h
}

function Hold-Key([byte]$vk, [int]$ms) {
  [Win32Shot]::keybd_event($vk, 0, 0, [UIntPtr]::Zero)
  Start-Sleep -Milliseconds $ms
  [Win32Shot]::keybd_event($vk, 0, 2, [UIntPtr]::Zero)   # KEYEVENTF_KEYUP
  Start-Sleep -Milliseconds 250
}

function Move-Mouse([int]$dx) {
  [Win32Shot]::mouse_event(1, [uint32]$dx, 0, 0, [UIntPtr]::Zero)   # MOUSEEVENTF_MOVE
  Start-Sleep -Milliseconds 500
}

$log = Join-Path $OutDir "player.log"
$p = Start-Process $Exe -ArgumentList @(
  "-screen-fullscreen", "0",
  "-screen-width", "$Width", "-screen-height", "$Height",
  "-logFile", $log
) -PassThru
Write-Host "launched pid=$($p.Id)"

try {
  Start-Sleep -Seconds $BootWaitSec
  if ($p.HasExited) { throw ("player exited early, code=" + $p.ExitCode) }

  $p.Refresh()
  [void][Win32Shot]::SetForegroundWindow($p.MainWindowHandle)
  Start-Sleep -Milliseconds 800
  $p.Refresh()
  Write-Host "window title='$($p.MainWindowTitle)' handle=$($p.MainWindowHandle)"
  # Keys are delivered to whatever holds foreground. If the game never took focus,
  # sending WASD would type into another app instead -- refuse rather than guess.
  if ($p.MainWindowHandle -eq [IntPtr]::Zero) { throw "player has no main window handle" }
  $fg = [Win32Shot]::GetForegroundWindow()
  if ($fg -ne $p.MainWindowHandle) { throw ("foreground is not the player (fg=$fg want=$($p.MainWindowHandle))") }
  Write-Host "foreground confirmed = player"

  $h0 = Take-Shot "01_start.png"
  Hold-Key 0x57 3000                       # W, 3s walk forward
  $h1 = Take-Shot "02_after_walk.png"
  Move-Mouse 700                          # yaw around
  $h2 = Take-Shot "03_look_around.png"
  Hold-Key 0x57 2500
  $h3 = Take-Shot "04_walk_again.png"
  Hold-Key 0x53 2500                      # S, back
  $h4 = Take-Shot "05_step_back.png"

  Write-Host "== frame hashes =="
  Write-Host "01 vs 02 : $(if ($h0 -ne $h1) { 'DIFFER (camera moved)' } else { 'SAME (no movement!)' })"
  Write-Host "02 vs 03 : $(if ($h1 -ne $h2) { 'DIFFER (look works)' } else { 'SAME (mouse look dead!)' })"
  Write-Host "03 vs 04 : $(if ($h2 -ne $h3) { 'DIFFER' } else { 'SAME' })"
  Write-Host "04 vs 05 : $(if ($h3 -ne $h4) { 'DIFFER' } else { 'SAME' })"

  $p.Refresh()
  Write-Host "still running: $(-not $p.HasExited)"

  # The build locks the cursor and has no pause menu, so Esc -> Application.Quit is the
  # player's only clean exit. Prove it actually terminates the process.
  # Re-assert foreground first: the last synthesized mouse-move can leave Windows holding a
  # different window on top, and then Esc would go to that app instead of the player --
  # which reads as "the quit binding is broken" when it is really "nobody sent the key".
  [void][Win32Shot]::SetForegroundWindow($p.MainWindowHandle)
  Start-Sleep -Milliseconds 800
  $p.Refresh()
  $fg2 = [Win32Shot]::GetForegroundWindow()
  if ($fg2 -ne $p.MainWindowHandle) { Write-Host ("ESC probe skipped: foreground is not the player (fg=" + $fg2 + ")") }
  else {
    Hold-Key 0x1B 120                                       # ESC
    # Poll instead of sleeping a fixed 3s: a Mono player's shutdown (asset unload + log flush)
    # can take longer than that, and a fixed sleep reports a clean quit as a failure.
    # 12s turned out to be too short as well (measured on the props build: the log already
    # shows Physics cleanup + Input System Shutdown while the poll is still waiting), so the
    # ceiling is a parameter. A timeout here means "still tearing down", NOT "quit is broken"
    # -- cross-check player.log for the Shutdown lines before believing this reading.
    $quitMs = 0
    while ($quitMs -lt $QuitWaitMs) {
      Start-Sleep -Milliseconds 500
      $quitMs += 500
      $p.Refresh()
      if ($p.HasExited) { break }
    }
    if ($p.HasExited) { Write-Host "ESC quit: OK ($quitMs ms, exit code $($p.ExitCode))" }
    else { Write-Host "ESC quit: FAILED - process still alive after $quitMs ms" }
  }
}
finally {
  if (-not $p.HasExited) { [void]$p.Kill(); Start-Sleep -Seconds 2 }
  Write-Host "killed pid=$($p.Id)"
  if (Test-Path $log) {
    Write-Host "== player.log: error-ish lines =="
    Get-Content $log | Select-String -Pattern "Exception|error|Error|FAIL|Missing" |
      Where-Object { $_ -notmatch "UnityException: 0x|GfxDevice" } |
      Select-Object -First 25 | ForEach-Object { $_.Line.Trim() }
  } else { Write-Host "no player.log at $log" }
}
