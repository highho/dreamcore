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
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);
}
"@

if (-not (Test-Path $Exe)) { throw "no exe: $Exe" }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$Exe = (Resolve-Path $Exe).Path
$OutDir = (Resolve-Path $OutDir).Path
$bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
Write-Host "desktop $($bounds.Width)x$($bounds.Height)"

function Get-MeanLuma($bmp) {
  # Sampled every 7th pixel: cheap enough for a 1700x1070 desktop grab, accurate enough to
  # tell a near-black menu page from a lit corridor. Frame *hashes* cannot make that call --
  # FilmGrain + lamp flicker make every pair of frames differ, so "00 vs 01 DIFFER" proves
  # only that the pixel data changed, not that a menu was left behind.
  $sum = [double]0; $n = 0
  for ($y = 0; $y -lt $bmp.Height; $y += 7) {
    for ($x = 0; $x -lt $bmp.Width; $x += 7) {
      $c = $bmp.GetPixel($x, $y)
      $sum += 0.299 * $c.R + 0.587 * $c.G + 0.114 * $c.B
      $n++
    }
  }
  return $sum / [double]$n
}

$script:lumaBy = @{}

function Take-Shot([string]$name) {
  $path = Join-Path $OutDir $name
  $bmp = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen(0, 0, 0, 0, $bmp.Size)
  $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
  $luma = Get-MeanLuma $bmp
  $g.Dispose(); $bmp.Dispose()
  $h = (Get-FileHash $path -Algorithm MD5).Hash
  $script:lumaBy[$name] = $luma
  Write-Host ("SHOT {0} md5={1} bytes={2} luma={3:F1}" -f $name, $h, (Get-Item $path).Length, $luma)
  return $h
}

function Hold-Key([byte]$vk, [int]$ms) {
  [Win32Shot]::keybd_event($vk, 0, 0, [UIntPtr]::Zero)
  Start-Sleep -Milliseconds $ms
  [Win32Shot]::keybd_event($vk, 0, 2, [UIntPtr]::Zero)   # KEYEVENTF_KEYUP
  Start-Sleep -Milliseconds 250
}

# Hold a key across several screenshots (Hold-Key releases too early for a frame burst).
function Key-Down([byte]$vk) { [Win32Shot]::keybd_event($vk, 0, 0, [UIntPtr]::Zero) }
function Key-Up([byte]$vk) { [Win32Shot]::keybd_event($vk, 0, 2, [UIntPtr]::Zero) }

function Move-Mouse([int]$dx, [int]$dy = 0) {
  [Win32Shot]::mouse_event(1, $dx, $dy, 0, [UIntPtr]::Zero)   # MOUSEEVENTF_MOVE (dx yaw, dy pitch)
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

  # M8: the build boots into Title.unity now. Shoot the title page, then send Enter
  # (TitleScreen also accepts Space/KeypadEnter). Pressing the key is the only way to prove
  # the title really starts the game -- a dead button leaves this script staring at the same
  # menu frame for the rest of the run, which would otherwise read as "the corridor is broken".
  $hTitle = Take-Shot "00_title.png"
  Hold-Key 0x0D 120                                    # VK_RETURN
  Start-Sleep -Seconds 2                               # Main3D load + first rendered frames
  if ($p.HasExited) { throw "player exited right after Enter: title -> game transition crashed" }
  Write-Host "sent Enter to leave the title screen"

  $h0 = Take-Shot "01_start.png"
  Hold-Key 0x57 3000                       # W, 3s walk forward
  $h1 = Take-Shot "02_after_walk.png"
  Move-Mouse 700                          # yaw around
  $h2 = Take-Shot "03_look_around.png"
  Hold-Key 0x57 2500
  $h3 = Take-Shot "04_walk_again.png"
  Hold-Key 0x53 2500                      # S, back
  $h4 = Take-Shot "05_step_back.png"

  # Mouse Y is a *smoothed* legacy axis (ProjectSettings sensitivity 0.1), so one small dy
  # event barely registers: 30 px damped to ~0.1 deg/frame read as "pitch is dead" in an
  # earlier probe while a 700 px yaw turned fine. Pitch in yaw-sized chunks instead --
  # 3x250 px drives it into the pitchLimit clamp, which is where the feet are in frame.
  Move-Mouse 0 250
  [void](Take-Shot "06_look_down_a.png")
  Move-Mouse 0 250
  [void](Take-Shot "07_look_down_b.png")
  Move-Mouse 0 250
  $h5 = Take-Shot "08_look_down_max.png"

  # The one-time hint bar holds 8s from scene start, then fades over 1.5s. Everything above
  # takes >=11s, so 09 is the "hint gone" frame; 01_start.png (~2s in) is the frame where the
  # bar is still on screen. Read the pair with the eye -- the hashes cannot tell you whether
  # a UI element disappeared, only that *something* on screen changed.
  $h9 = Take-Shot "09_hint_gone.png"

  # M7 legs: burst 4 frames while W is held.
  # At walkSpeed 1.6 and stepDistance 1.5 one footfall is ~0.94s, so frames 220ms apart
  # land on different leg angles -- a frozen rig (or legs that don't swing at all) reads
  # as SAME. 14 is taken after releasing W: legBlend damps to 0 in ~0.2s, so the pose
  # must come back to the neutral one it had in 08.
  Key-Down 0x57
  Start-Sleep -Milliseconds 400                       # let legBlend ramp up
  $l0 = Take-Shot "10_leg_a.png"
  Start-Sleep -Milliseconds 220
  $l1 = Take-Shot "11_leg_b.png"
  Start-Sleep -Milliseconds 220
  $l2 = Take-Shot "12_leg_c.png"
  Start-Sleep -Milliseconds 220
  $l3 = Take-Shot "13_leg_d.png"
  Key-Up 0x57
  Start-Sleep -Milliseconds 700
  $l4 = Take-Shot "14_leg_rest.png"
  Write-Host "== leg burst (220ms apart, walking) =="
  # Do NOT read these hashes as proof the legs swung: FilmGrain is temporal noise and the
  # six lamps flicker, so any two frames of this player differ even with a frozen rig.
  # The burst is evidence for the eye -- 10..13 vs 14/06 is what settles "legs move".
  Write-Host "hashes below are for the record only (grain+flicker make every pair differ)"
  Write-Host "08(rest) 10 11 12 13 14(rest):"
  Write-Host ("  " + $h5 + " " + $l0 + " " + $l1 + " " + $l2 + " " + $l3 + " " + $l4)

  Write-Host "== frame hashes =="
  $l00 = $script:lumaBy["00_title.png"]; $l01 = $script:lumaBy["01_start.png"]
  # The title page is a near-black menu; the corridor is lit. A ratio this side of 2x means
  # frame 00 was not the menu at all (someone/something left it before the screenshot).
  Write-Host ("00 luma={0:F1}  01 luma={1:F1}" -f $l00, $l01)
  Write-Host "00 vs 01 : $(if ($l00 -lt $l01 * 0.5) { 'DARK -> BRIGHT (Enter switched title -> corridor)' } else { 'SUSPECT (frame 00 is not the dark title page -- menu was already left before the shot!)' })"
  Write-Host "00 vs 01 (hash) : $(if ($hTitle -ne $h0) { 'DIFFER' } else { 'SAME (Enter did nothing -- title is a dead end!)' })"
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
